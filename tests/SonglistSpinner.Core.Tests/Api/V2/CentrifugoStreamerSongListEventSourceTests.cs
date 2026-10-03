using System.Net.WebSockets;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api.V2;

public class CentrifugoStreamerSongListEventSourceTests
{
    private const int StreamerId = 314;
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    private static readonly StreamerSongListEventsOptions Options = new()
    {
        Endpoint = new Uri("wss://events.example.test/connection/websocket"),
        InitialReconnectDelay = TimeSpan.FromSeconds(1),
        MaximumReconnectDelay = TimeSpan.FromSeconds(3),
        ReceiveIdleTimeout = TimeSpan.FromMinutes(5)
    };

    [Fact]
    public void Given_DefaultOptions_When_ConstructingEventSource_Then_UsesProductionWebSocketEndpoint()
    {
        var options = new StreamerSongListEventsOptions();

        _ = new CentrifugoStreamerSongListEventSource(options);

        Assert.Equal(
            "wss://events.streamersonglist.com/connection/websocket",
            options.Endpoint.AbsoluteUri);
    }

    [Fact]
    public void Given_HttpEndpoint_When_ConstructingEventSource_Then_RejectsOptions()
    {
        var options = new StreamerSongListEventsOptions
        {
            Endpoint = new Uri("https://events.example.test/connection/websocket")
        };

        var exception = Assert.Throws<ArgumentException>(() => new CentrifugoStreamerSongListEventSource(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Equal(
            "The StreamerSongList event endpoint must be an absolute WebSocket URI. (Parameter 'options')",
            exception.Message);
    }

    [Fact]
    public void Given_NonPositiveReceiveTimeout_When_ConstructingEventSource_Then_RejectsOptions()
    {
        var options = new StreamerSongListEventsOptions { ReceiveIdleTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CentrifugoStreamerSongListEventSource(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Equal("The receive idle timeout must be positive. (Parameter 'options')", exception.Message);
    }

    [Fact]
    public async Task Given_ServerAcceptsTheConnection_When_Subscribing_Then_ConnectsAndSubscribesToBothStreamerChannels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var socket = ScriptedWebSocket.AcceptingSubscriptions();
        var connector = new ScriptedConnector().Accept(socket);
        var source = CreateSource(connector, new FakeTimeProvider());
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var first = await NextAsync(events, cancellationToken);

        Assert.Equal(StreamerSongListEventKind.Connected, first.Kind);
        Assert.Equal([Options.Endpoint], connector.Endpoints);
        Assert.Equal(
            [
                """{"id":1,"connect":{}}""",
                """{"id":2,"subscribe":{"channel":"streamer:314-queue"}}""",
                """{"id":3,"subscribe":{"channel":"streamer:314-play_history"}}"""
            ],
            socket.Sent);
    }

    [Fact]
    public async Task Given_PublicationArrivesBeforeSubscriptionsAreConfirmed_When_Subscribing_Then_DeliversItAfterConnected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var socket = ScriptedWebSocket.AcceptingSubscriptions(Publication("queue_add"));
        var source = CreateSource(new ScriptedConnector().Accept(socket), new FakeTimeProvider());
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var first = await NextAsync(events, cancellationToken);
        var second = await NextAsync(events, cancellationToken);

        Assert.Equal(new StreamerSongListEvent(StreamerSongListEventKind.Connected), first);
        Assert.Equal(new StreamerSongListEvent(StreamerSongListEventKind.QueueChanged, "queue_add"), second);
    }

    [Fact]
    public async Task Given_ConnectedSubscription_When_ServerPublishesAPlayHistoryChange_Then_YieldsPlayHistoryChanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var socket = ScriptedWebSocket.AcceptingSubscriptions();
        var source = CreateSource(new ScriptedConnector().Accept(socket), new FakeTimeProvider());
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        await NextAsync(events, cancellationToken);

        socket.Push(Publication("play_history_add"));
        var notification = await NextAsync(events, cancellationToken);

        Assert.Equal(
            new StreamerSongListEvent(StreamerSongListEventKind.PlayHistoryChanged, "play_history_add"),
            notification);
    }

    [Fact]
    public async Task Given_ConnectedSubscription_When_ServerSendsAnApplicationPing_Then_RepliesWithAnEmptyObject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var socket = ScriptedWebSocket.AcceptingSubscriptions();
        var source = CreateSource(new ScriptedConnector().Accept(socket), new FakeTimeProvider());
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        await NextAsync(events, cancellationToken);

        socket.Push("{}");
        socket.Push(Publication("queue_update"));
        await NextAsync(events, cancellationToken);

        Assert.Equal(4, socket.Sent.Count);
        Assert.Equal("{}", socket.Sent[^1]);
    }

    [Fact]
    public async Task Given_ServerClosesTheConnection_When_Subscribed_Then_ReportsReconnectingAndReconnectsAfterTheInitialDelay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var closing = ScriptedWebSocket.AcceptingSubscriptions();
        closing.CloseFromServer();
        var connector = new ScriptedConnector().Accept(closing).Accept(ScriptedWebSocket.AcceptingSubscriptions());
        var source = CreateSource(connector, time);
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        await NextAsync(events, cancellationToken);

        var reconnecting = await NextAsync(events, cancellationToken);
        var (delay, reconnected) = await AdvancePastReconnectDelayAsync(events, time, cancellationToken);

        Assert.Equal(
            new StreamerSongListEvent(
                StreamerSongListEventKind.Reconnecting,
                Error: "The StreamerSongList event server closed the connection."),
            reconnecting);
        Assert.True(closing.IsDisposed);
        Assert.Equal(TimeSpan.FromSeconds(1), delay);
        Assert.Equal(StreamerSongListEventKind.Connected, reconnected.Kind);
        Assert.Equal(2, connector.Endpoints.Count);
    }

    [Fact]
    public async Task Given_ConnectionAttemptsKeepFailing_When_Reconnecting_Then_DelayDoublesUpToTheMaximum()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var connector = new ScriptedConnector()
            .Refuse("refused 1")
            .Refuse("refused 2")
            .Refuse("refused 3")
            .Refuse("refused 4")
            .Accept(ScriptedWebSocket.AcceptingSubscriptions());
        var source = CreateSource(connector, time);
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        var received = new List<StreamerSongListEvent> { await NextAsync(events, cancellationToken) };
        var delays = new List<TimeSpan>();

        for (var retry = 0; retry < 4; retry++)
        {
            var (delay, next) = await AdvancePastReconnectDelayAsync(events, time, cancellationToken);
            delays.Add(delay);
            received.Add(next);
        }

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)],
            delays);
        Assert.Equal(
            [
                new StreamerSongListEvent(StreamerSongListEventKind.Reconnecting, Error: "refused 1"),
                new StreamerSongListEvent(StreamerSongListEventKind.Reconnecting, Error: "refused 2"),
                new StreamerSongListEvent(StreamerSongListEventKind.Reconnecting, Error: "refused 3"),
                new StreamerSongListEvent(StreamerSongListEventKind.Reconnecting, Error: "refused 4"),
                new StreamerSongListEvent(StreamerSongListEventKind.Connected)
            ],
            received);
    }

    [Fact]
    public async Task Given_ConnectionRecoveredAfterFailures_When_ItDropsAgain_Then_DelayRestartsFromTheInitialDelay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var closing = ScriptedWebSocket.AcceptingSubscriptions();
        closing.CloseFromServer();
        var connector = new ScriptedConnector()
            .Refuse("refused 1")
            .Refuse("refused 2")
            .Accept(closing)
            .Accept(ScriptedWebSocket.AcceptingSubscriptions());
        var source = CreateSource(connector, time);
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        await NextAsync(events, cancellationToken);
        var failedDelays = new List<TimeSpan>
        {
            (await AdvancePastReconnectDelayAsync(events, time, cancellationToken)).Delay,
            (await AdvancePastReconnectDelayAsync(events, time, cancellationToken)).Delay
        };
        await NextAsync(events, cancellationToken);

        var (delay, reconnected) = await AdvancePastReconnectDelayAsync(events, time, cancellationToken);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], failedDelays);
        Assert.Equal(TimeSpan.FromSeconds(1), delay);
        Assert.Equal(StreamerSongListEventKind.Connected, reconnected.Kind);
    }

    [Fact]
    public async Task Given_ConnectedSubscription_When_NoDataArrivesWithinTheIdleTimeout_Then_ReportsReconnecting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var socket = ScriptedWebSocket.AcceptingSubscriptions();
        var source = CreateSource(new ScriptedConnector().Accept(socket), time);
        await using var events = source.SubscribeAsync(StreamerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        await NextAsync(events, cancellationToken);
        // The pending receive starts its idle timer before MoveNextAsync returns.
        var next = events.MoveNextAsync().AsTask();

        time.Advance(Options.ReceiveIdleTimeout);

        Assert.True(await next.WaitAsync(WaitLimit, cancellationToken));
        Assert.Equal(
            new StreamerSongListEvent(
                StreamerSongListEventKind.Reconnecting,
                Error: "No StreamerSongList event data was received for 300 seconds."),
            events.Current);
        Assert.True(socket.IsDisposed);
    }

    private static CentrifugoStreamerSongListEventSource CreateSource(
        ScriptedConnector connector,
        TimeProvider timeProvider) =>
        new(Options, timeProvider, connector.ConnectAsync);

    private static async Task<StreamerSongListEvent> NextAsync(
        IAsyncEnumerator<StreamerSongListEvent> events,
        CancellationToken cancellationToken)
    {
        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(WaitLimit, cancellationToken));
        return events.Current;
    }

    /// <summary>
    /// Asks for the next event, which starts the reconnect delay, then moves the clock past that delay.
    /// </summary>
    private static async Task<(TimeSpan Delay, StreamerSongListEvent Next)> AdvancePastReconnectDelayAsync(
        IAsyncEnumerator<StreamerSongListEvent> events,
        TimerTrackingTimeProvider time,
        CancellationToken cancellationToken)
    {
        var next = events.MoveNextAsync().AsTask();
        var delay = await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken);
        time.Advance(delay);

        Assert.True(await next.WaitAsync(WaitLimit, cancellationToken));
        return (delay, events.Current);
    }

    private static string Publication(string eventType) =>
        $$$$$"""{"push":{"channel":"streamer:314-queue","pub":{"data":{"type":"{{{{{eventType}}}}}","data":null}}}}""";

    /// <summary>Hands out a scripted result for each connection attempt and records the endpoint used.</summary>
    private sealed class ScriptedConnector
    {
        private readonly Queue<Func<Task<WebSocket>>> _attempts = new();
        private readonly List<Uri> _endpoints = [];

        public IReadOnlyList<Uri> Endpoints
        {
            get
            {
                lock (_endpoints)
                    return [.. _endpoints];
            }
        }

        public ScriptedConnector Accept(ScriptedWebSocket socket)
        {
            _attempts.Enqueue(() => Task.FromResult<WebSocket>(socket));
            return this;
        }

        public ScriptedConnector Refuse(string error)
        {
            _attempts.Enqueue(() => Task.FromException<WebSocket>(new WebSocketException(error)));
            return this;
        }

        public Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            Func<Task<WebSocket>>? attempt;
            lock (_endpoints)
            {
                _endpoints.Add(endpoint);
                _attempts.TryDequeue(out attempt);
            }

            return attempt?.Invoke() ??
                   Task.FromException<WebSocket>(new WebSocketException("No further connection was scripted."));
        }
    }
}
