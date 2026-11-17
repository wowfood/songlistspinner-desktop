using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using SonglistSpinner.Testing;
using Xunit;
using static SonglistSpinner.Application.Tests.ScriptedStreamerSongListClient;

namespace SonglistSpinner.Application.Tests.Services;

public class StreamerSessionServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Given_RealtimeRefreshFails_When_NoFurtherEventArrives_Then_RetriesUntilRefreshSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => throw new IOException("Simulated transient API failure"));
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41)));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService(), time);
        var recovered = WaitForRecoveryAfterFailure(session);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.QueueChanged);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        var snapshot = await recovered.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(2, api.QueueFetches);
        Assert.Equal(StreamerSessionHealth.Healthy, snapshot.ApiHealth);
        Assert.Equal([41], snapshot.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RealtimeRefreshTimesOut_When_NoFurtherEventArrives_Then_ReportsTheFailureAndRetriesUntilRefreshSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        // How an HttpClient timeout arrives: a cancellation the session's own token did not ask for.
        api.QueueResponses.Enqueue(_ => throw new TaskCanceledException("The request timed out.", new TimeoutException()));
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41)));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService(), time);
        var recovered = WaitForRecoveryAfterFailure(session);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.QueueChanged);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        var snapshot = await recovered.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(2, api.QueueFetches);
        Assert.Equal([41], snapshot.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RealtimeRefreshKeepsFailing_When_Retrying_Then_DelayDoublesFromOneSecondUpToThirtySeconds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        for (var i = 0; i < 6; i++)
            api.QueueResponses.Enqueue(_ => throw new IOException("Simulated transient API failure"));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService(), time);
        var recovered = WaitForRecoveryAfterFailure(session);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.QueueChanged);
        var delays = new List<TimeSpan>();
        for (var i = 0; i < 7; i++)
            delays.Add(await AdvancePastNextTimerAsync(time, cancellationToken));
        await recovered.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)
            ],
            delays);
        Assert.Equal(7, api.QueueFetches);
    }

    [Fact]
    public async Task Given_ChannelStarted_When_ReadingTheSnapshot_Then_ApiHealthReportsTheClockTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var startTime = new DateTimeOffset(2026, 10, 3, 14, 5, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(startTime);
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            new ChannelEventSource(),
            new OverlayStateService(),
            time);

        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        Assert.Equal(
            $"Queue and history last synchronized at {startTime:t}.",
            session.GetSnapshot().ApiHealthDetail);
    }

    [Fact]
    public async Task Given_RefreshSuspended_When_Refreshing_Then_SkipsTheApiAndKeepsTheSnapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        session.SetRefreshSuspended(true);

        var refreshed = await session.RefreshAsync(Streamer, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal(0, api.QueueFetches);
        Assert.Equal([7], session.GetSnapshot().AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RefreshInFlight_When_SpinSuspendsRefresh_Then_FetchedQueueIsNotPublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        var pendingQueue = new TaskCompletionSource<SpinnerQueueSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        api.QueueResponses.Enqueue(_ => pendingQueue.Task);
        var overlay = new OverlayStateService();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), overlay);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        var refresh = session.RefreshAsync(Streamer, cancellationToken);
        await api.QueueFetchStarted.Task.WaitAsync(WaitLimit, cancellationToken);

        session.SetRefreshSuspended(true);
        pendingQueue.SetResult(QueueWith(41, 42));
        var refreshed = await refresh.WaitAsync(WaitLimit, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal([7], session.GetSnapshot().AvailableSongs.Select(song => song.QueueId));
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal(1, overlayState.RootElement.GetProperty("availableCount").GetInt32());
    }

    [Fact]
    public async Task Given_ExcludePlayedSongsTurnedOnDuringRefresh_When_FetchCompletes_Then_FiltersWithTheLatestConfig()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        var pendingQueue = new TaskCompletionSource<SpinnerQueueSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        api.QueueResponses.Enqueue(_ => pendingQueue.Task);
        api.PlayHistory = [new PlayHistoryItem { Song = new SpinnerSong { Id = 500 } }];
        var includePlayed = new SpinnerConfig { PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = false } };
        var excludePlayed = new SpinnerConfig { PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true } };
        // The fake clock never fires the debounced refresh that the config change requests.
        await using var session = new StreamerSessionService(
            api,
            new ChannelEventSource(),
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, includePlayed, [], [], null, cancellationToken);
        var refresh = session.RefreshAsync(Streamer, cancellationToken);
        await api.QueueFetchStarted.Task.WaitAsync(WaitLimit, cancellationToken);

        session.UpdateConfig(excludePlayed);
        pendingQueue.SetResult(new SpinnerQueueSnapshot
        {
            Items =
            [
                new SpinnerQueueItem { QueueId = 41, Song = new SpinnerSong { Id = 500 } },
                new SpinnerQueueItem { QueueId = 42, Song = new SpinnerSong { Id = 501 } }
            ]
        });
        var refreshed = await refresh.WaitAsync(WaitLimit, cancellationToken);

        Assert.NotNull(refreshed);
        Assert.Equal([42], refreshed.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RefreshSuspended_When_PublishingAnEmptyQueue_Then_OverlayShowsNoSongs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            new ChannelEventSource(),
            overlay);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        session.SetRefreshSuspended(true);

        session.UpdateSnapshot(new SpinnerConfig(), [], [], null);

        Assert.Empty(session.GetSnapshot().AvailableSongs);
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal(0, overlayState.RootElement.GetProperty("availableCount").GetInt32());
    }

    [Fact]
    public async Task Given_RealtimeConnects_When_TheDebounceDelayPasses_Then_RefreshesTheQueueAndReportsRealtimeHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41)));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService(), time);
        // The session records realtime health before it signals the refresh, so the first change that carries
        // the refreshed songs must already say Healthy. Waiting on the songs alone is what checks that order.
        var refreshed = WaitForChange(session, change => change.Snapshot.AvailableSongs.Length == 1);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.Connected);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        var snapshot = (await refreshed.WaitAsync(WaitLimit, cancellationToken)).Snapshot;

        Assert.Equal(1, api.QueueFetches);
        Assert.Equal([41], snapshot.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(StreamerSessionHealth.Healthy, snapshot.RealtimeHealth);
        Assert.Equal("Receiving live queue and history events.", snapshot.RealtimeHealthDetail);
    }

    [Fact]
    public async Task Given_RealtimeConnection_When_ItIsReconnecting_Then_RealtimeHealthIsDegradedWithTheServersReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        var degraded = WaitForChange(session, change => change.Snapshot.RealtimeHealth == StreamerSessionHealth.Degraded);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.Reconnecting, "socket dropped");
        var change = await degraded.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal("socket dropped", change.Snapshot.RealtimeHealthDetail);
        Assert.Equal("Realtime updates disconnected; reconnecting...", change.Announcement);
    }

    [Fact]
    public async Task Given_RealtimeWasReconnecting_When_ItConnectsAgain_Then_AnnouncesItReconnected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        var reconnected = WaitForChange(session, change => change.Announcement == "Realtime updates reconnected.");
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.Connected);
        events.Publish(StreamerSongListEventKind.Reconnecting, "socket dropped");
        events.Publish(StreamerSongListEventKind.Connected);
        var change = await reconnected.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(StreamerSessionHealth.Healthy, change.Snapshot.RealtimeHealth);
    }

    [Fact]
    public async Task Given_RealtimeConnection_When_TheEventStreamFails_Then_RealtimeHealthIsFailedWithTheError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        var failed = WaitForChange(session, change => change.Snapshot.RealtimeHealth == StreamerSessionHealth.Failed);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Fail(new InvalidOperationException("event stream broke"));
        var change = await failed.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal("event stream broke", change.Snapshot.RealtimeHealthDetail);
        Assert.Equal("Realtime updates stopped: event stream broke", change.Announcement);
    }

    [Fact]
    public async Task Given_ExcludePlayedSongsOn_When_APlayHistoryChangeArrives_Then_TheNewlyPlayedSongLeavesTheWheel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        SpinnerQueueItem[] queue = [SongWithId(41, songId: 500), SongWithId(42, songId: 501)];
        api.QueueResponses.Enqueue(_ => Task.FromResult(new SpinnerQueueSnapshot { Items = [.. queue] }));
        api.PlayHistory = [new PlayHistoryItem { Song = new SpinnerSong { Id = 500 } }];
        var events = new ChannelEventSource();
        var overlay = new OverlayStateService();
        var config = new SpinnerConfig { PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true } };
        await using var session = new StreamerSessionService(api, events, overlay, time);
        var refreshed = WaitForChange(session, change => change.Snapshot.PlayedSongs.Length == 1);
        await session.StartAsync(1, Streamer, config, queue, [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.PlayHistoryChanged);
        await AdvancePastNextTimerAsync(time, cancellationToken);
        var snapshot = (await refreshed.WaitAsync(WaitLimit, cancellationToken)).Snapshot;

        Assert.Equal([42], snapshot.AvailableSongs.Select(song => song.QueueId));
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal(1, overlayState.RootElement.GetProperty("availableCount").GetInt32());
        Assert.Equal(1, overlayState.RootElement.GetProperty("playedCount").GetInt32());
    }

    [Fact]
    public async Task Given_ABurstOfRealtimeEvents_When_TheDebounceDelayPasses_Then_OneFetchCoversTheBurst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42, 43)));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService(), time);
        var refreshed = WaitForChange(session, change => change.Snapshot.AvailableSongs.Length == 3);
        // Published before the subscription starts, so the whole burst is waiting when the session reads it.
        events.Publish(StreamerSongListEventKind.QueueChanged);
        events.Publish(StreamerSongListEventKind.QueueChanged);
        events.Publish(StreamerSongListEventKind.PlayHistoryChanged);

        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);
        var debounce = await AdvancePastNextTimerAsync(time, cancellationToken);
        await refreshed.WaitAsync(WaitLimit, cancellationToken);
        // Clearing waits for the refresh loop to end. A loop that debounces again for a leftover signal has
        // normally created that timer by then; one cancelled first creates none, so this check can miss a
        // regression on some runs but never fails without one.
        await session.ClearAsync(new SpinnerConfig());

        Assert.Equal(TimeSpan.FromMilliseconds(300), debounce);
        Assert.Equal(1, api.QueueFetches);
        Assert.False(time.TryTakeCreatedTimer(out _), "The burst started a second debounce.");
    }

    [Fact]
    public async Task Given_AnotherChannelIsLoaded_When_RefreshingThePreviousChannel_Then_NothingIsFetchedOrPublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        await session.StartAsync(2, "another-channel", new SpinnerConfig(), [Song(7)], [], null, cancellationToken);

        var refreshed = await session.RefreshAsync(Streamer, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal(0, api.QueueFetches);
        Assert.Equal("another-channel", session.GetSnapshot().Streamer);
    }

    [Fact]
    public async Task Given_ALoadedChannel_When_Cleared_Then_TheOverlayShowsNoStreamerAndRealtimeEventsStop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var events = new ChannelEventSource();
        var overlay = new OverlayStateService();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            overlay,
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);

        await session.ClearAsync(new SpinnerConfig());

        Assert.True(events.SubscriptionEnded.IsCompleted);
        Assert.False(session.GetSnapshot().HasChannel);
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal("", overlayState.RootElement.GetProperty("streamer").GetString());
        Assert.Equal(0, overlayState.RootElement.GetProperty("availableCount").GetInt32());
        Assert.Equal(
            ["Waiting for Dashboard..."],
            overlayState.RootElement.GetProperty("wheelItems").EnumerateArray()
                .Select(item => item.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Given_ASpinSuspendedRefreshes_When_TheStreamerIsChangedAndAnotherLoaded_Then_TheNewChannelRefreshes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41)));
        await using var session = new StreamerSessionService(
            api,
            new ChannelEventSource(),
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        session.SetRefreshSuspended(true);

        await session.ClearAsync(new SpinnerConfig());
        await session.StartAsync(2, "another-channel", new SpinnerConfig(), [], [], null, cancellationToken);
        var refreshed = await session.RefreshAsync("another-channel", cancellationToken);

        Assert.NotNull(refreshed);
        Assert.Equal([41], refreshed.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_ALoadedChannel_When_StartingAnotherIsAlreadyCancelled_Then_ThePreviousChannelKeepsItsSubscription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.StartAsync(
            2,
            "another-channel",
            new SpinnerConfig(),
            [],
            [],
            null,
            new CancellationToken(canceled: true)));

        Assert.Equal(Streamer, session.GetSnapshot().Streamer);
        Assert.False(events.SubscriptionEnded.IsCompleted);
    }

    [Fact]
    public async Task Given_ALoadedChannel_When_StartingAnotherIsCancelledWhileThePreviousSubscriptionStops_Then_TheNewChannelIsLive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var previousSubscriptionHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ChannelEventSource { HoldSubscriptionEnd = previousSubscriptionHeld.Task };
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        var live = WaitForChange(
            session,
            change => change.Snapshot.Streamer == "another-channel" &&
                      change.Snapshot.RealtimeHealth == StreamerSessionHealth.Healthy);
        using var callerLifetime = new CancellationTokenSource();

        Task start;
        try
        {
            start = session.StartAsync(2, "another-channel", new SpinnerConfig(), [], [], null, callerLifetime.Token);
            await events.SubscriptionEnded.WaitAsync(WaitLimit, cancellationToken);
            callerLifetime.Cancel();
        }
        finally
        {
            previousSubscriptionHeld.TrySetResult();
        }

        await start.WaitAsync(WaitLimit, cancellationToken);
        events.Publish(StreamerSongListEventKind.Connected);
        await live.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal("another-channel", session.GetSnapshot().Streamer);
    }

    [Fact]
    public async Task Given_AStartIsStillStoppingThePreviousSubscription_When_TheSessionIsCleared_Then_TheClearTakesEffectAfterTheStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var previousSubscriptionHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ChannelEventSource { HoldSubscriptionEnd = previousSubscriptionHeld.Task };
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);

        Task start;
        Task clear;
        try
        {
            start = session.StartAsync(2, "another-channel", new SpinnerConfig(), [], [], null, cancellationToken);
            await events.SubscriptionEnded.WaitAsync(WaitLimit, cancellationToken);
            clear = session.ClearAsync(new SpinnerConfig());
        }
        finally
        {
            previousSubscriptionHeld.TrySetResult();
        }

        await Task.WhenAll(start, clear).WaitAsync(WaitLimit, cancellationToken);

        Assert.False(session.GetSnapshot().HasChannel);
    }

    [Fact]
    public async Task Given_ClearsQueuedBehindADispose_When_TheSessionIsDisposed_Then_EachFailsWithoutChangingTheSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var previousSubscriptionHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ChannelEventSource { HoldSubscriptionEnd = previousSubscriptionHeld.Task };
        var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            events,
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);

        Task start;
        Task dispose;
        Task firstClear;
        Task secondClear;
        try
        {
            // The start holds the session while the previous subscription stops; the rest queue behind it.
            start = session.StartAsync(2, "another-channel", new SpinnerConfig(), [], [], null, cancellationToken);
            await events.SubscriptionEnded.WaitAsync(WaitLimit, cancellationToken);
            dispose = session.DisposeAsync().AsTask();
            firstClear = session.ClearAsync(new SpinnerConfig());
            secondClear = session.ClearAsync(new SpinnerConfig());
        }
        finally
        {
            previousSubscriptionHeld.TrySetResult();
        }

        await Task.WhenAll(start, dispose).WaitAsync(WaitLimit, cancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => firstClear.WaitAsync(WaitLimit, cancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => secondClear.WaitAsync(WaitLimit, cancellationToken));

        Assert.Equal("another-channel", session.GetSnapshot().Streamer);
    }

    [Fact]
    public async Task Given_ADisposedSession_When_DisposedAgain_Then_NothingHappens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            new ChannelEventSource(),
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        await session.DisposeAsync();

        await session.DisposeAsync();

        Assert.Equal(Streamer, session.GetSnapshot().Streamer);
    }

    [Fact]
    public async Task Given_AnObserverThrows_When_TheSessionChanges_Then_LaterObserversAreStillNotified()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = new StreamerSessionService(
            new ScriptedStreamerSongListClient(),
            new ChannelEventSource(),
            new OverlayStateService(),
            new FakeTimeProvider());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);
        var notified = new List<StreamerSessionSnapshot>();
        session.Changed += (_, _) => throw new InvalidOperationException("Observer failed");
        session.Changed += (_, change) => notified.Add(change.Snapshot);
        var updated = new SpinnerConfig { PlayHistory = new SpinnerPlayHistoryConfig { Period = "month" } };

        session.UpdateConfig(updated);

        Assert.Same(updated, Assert.Single(notified).Config);
    }

    private static SpinnerQueueItem SongWithId(int queueId, int songId) =>
        new() { QueueId = queueId, Song = new SpinnerSong { Id = songId } };

    private static Task<StreamerSessionChange> WaitForChange(
        StreamerSessionService session,
        Func<StreamerSessionChange, bool> predicate)
    {
        var matched = new TaskCompletionSource<StreamerSessionChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, change) =>
        {
            if (predicate(change)) matched.TrySetResult(change);
        };
        return matched.Task;
    }

    /// <summary>Waits for the session to start its next delay, then moves the clock past it.</summary>
    private static async Task<TimeSpan> AdvancePastNextTimerAsync(
        TimerTrackingTimeProvider time,
        CancellationToken cancellationToken)
    {
        var dueTime = await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken);
        time.Advance(dueTime);
        return dueTime;
    }

    private static Task<StreamerSessionSnapshot> WaitForRecoveryAfterFailure(StreamerSessionService session)
    {
        var recovered = new TaskCompletionSource<StreamerSessionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = false;
        session.Changed += (_, e) =>
        {
            if (e.Snapshot.ApiHealth == StreamerSessionHealth.Failed)
                failed = true;
            else if (failed && e.Snapshot.ApiHealth == StreamerSessionHealth.Healthy)
                recovered.TrySetResult(e.Snapshot);
        };
        return recovered.Task;
    }
}
