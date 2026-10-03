using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Simulator;
using SonglistSpinner.Testing;
using Xunit;
using static SonglistSpinner.IntegrationTests.SimulatorClients;

namespace SonglistSpinner.IntegrationTests.StreamerSongList.Api.V2;

public class CentrifugoStreamerSongListEventSourceTests
{
    private const int StreamerId = 314;

    [Fact]
    public async Task Given_Subscribed_When_AViewerRequestsASong_Then_YieldsAQueueChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood", streamerId: StreamerId);
        var source = CreateEventSource(simulator, new FakeTimeProvider());
        await using var events = source.SubscribeAsync(new StreamerId(StreamerId), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        Assert.Equal(StreamerSongListEventKind.Connected, (await NextAsync(events)).Kind);

        await channel.RequestSongAsync("Toto", "Africa", "viewer");

        Assert.Equal(
            new StreamerSongListEvent(StreamerSongListEventKind.QueueChanged, "queue_add"),
            await NextAsync(events));
    }

    [Fact]
    public async Task Given_Subscribed_When_TheStreamerMarksASongPlayedElsewhere_Then_YieldsAQueueChangeThenAPlayHistoryChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood", streamerId: StreamerId);
        var entry = await channel.RequestSongAsync("Toto", "Africa", "viewer");
        var source = CreateEventSource(simulator, new FakeTimeProvider());
        await using var events = source.SubscribeAsync(new StreamerId(StreamerId), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        Assert.Equal(StreamerSongListEventKind.Connected, (await NextAsync(events)).Kind);

        await channel.MarkPlayedAsync(entry.QueueId);

        Assert.Equal(
            new StreamerSongListEvent(StreamerSongListEventKind.QueueChanged, "queue_remove"),
            await NextAsync(events));
        Assert.Equal(
            new StreamerSongListEvent(StreamerSongListEventKind.PlayHistoryChanged, "play_history_add"),
            await NextAsync(events));
    }

    [Fact]
    public async Task Given_Subscribed_When_TheSocketIsDropped_Then_ReconnectsAfterTheInitialDelayAndKeepsDeliveringChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood", streamerId: StreamerId);
        var time = new TimerTrackingTimeProvider();
        var source = CreateEventSource(simulator, time);
        await using var events = source.SubscribeAsync(new StreamerId(StreamerId), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        Assert.Equal(StreamerSongListEventKind.Connected, (await NextAsync(events)).Kind);

        simulator.DropEventConnections();
        var disconnected = await NextAsync(events);
        // The reconnect delay starts only once the consumer asks for the next event.
        var reconnected = events.MoveNextAsync().AsTask();
        await AdvancePastReconnectDelayAsync(time);
        await reconnected.WaitAsync(WaitLimit, cancellationToken);
        var afterReconnect = events.Current;
        await channel.RequestSongAsync("Toto", "Africa", "viewer");

        Assert.Equal(StreamerSongListEventKind.Reconnecting, disconnected.Kind);
        Assert.False(string.IsNullOrWhiteSpace(disconnected.Error));
        Assert.Equal(StreamerSongListEventKind.Connected, afterReconnect.Kind);
        Assert.Equal(StreamerSongListEventKind.QueueChanged, (await NextAsync(events)).Kind);
    }

    [Fact]
    public async Task Given_Subscribed_When_TheServerPings_Then_TheClientAnswersAndStaysSubscribed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood", streamerId: StreamerId);
        var source = CreateEventSource(simulator, new FakeTimeProvider());
        await using var events = source.SubscribeAsync(new StreamerId(StreamerId), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        Assert.Equal(StreamerSongListEventKind.Connected, (await NextAsync(events)).Kind);
        // The client reads, and so answers pings, only while the consumer is waiting for an event.
        var next = events.MoveNextAsync().AsTask();

        await simulator.PingEventConnectionsAsync(cancellationToken).WaitAsync(WaitLimit, cancellationToken);
        await channel.RequestSongAsync("Toto", "Africa", "viewer");

        Assert.True(await next.WaitAsync(WaitLimit, cancellationToken));
        Assert.Equal(StreamerSongListEventKind.QueueChanged, events.Current.Kind);
    }

    private static async Task<StreamerSongListEvent> NextAsync(IAsyncEnumerator<StreamerSongListEvent> events)
    {
        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(WaitLimit, TestContext.Current.CancellationToken));
        return events.Current;
    }

    /// <summary>
    /// Waits for the reconnect delay's timer, skipping the receive-idle timers the source starts per read, then
    /// moves the clock past it.
    /// </summary>
    private static async Task AdvancePastReconnectDelayAsync(TimerTrackingTimeProvider time)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        while (await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken) !=
               InitialReconnectDelay)
        {
        }

        time.Advance(InitialReconnectDelay);
    }
}
