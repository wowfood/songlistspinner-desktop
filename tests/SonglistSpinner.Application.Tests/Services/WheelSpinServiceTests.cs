using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using SonglistSpinner.Testing;
using Xunit;
using static SonglistSpinner.Application.Tests.ScriptedStreamerSongListClient;

namespace SonglistSpinner.Application.Tests.Services;

public class WheelSpinServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Given_QueueHasSongs_When_Drawing_Then_WinnerIsTheSongThePickerChooses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42, 43)));
        await using var session = await StartSessionAsync(api, new OverlayStateService(), Streamer);
        var spins = new WheelSpinService(api, session, new OverlayStateService(), new FixedRandom(1));

        var draw = await spins.DrawAsync(Streamer, new SpinnerConfig(), cancellationToken);

        Assert.Equal([41, 42, 43], draw.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(1, draw.WinnerIndex);
        Assert.Equal(42, draw.Winner?.QueueId);
    }

    [Fact]
    public async Task Given_QueueIsEmpty_When_Drawing_Then_ThereIsNoWinner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var session = await StartSessionAsync(api, new OverlayStateService(), Streamer);
        var spins = new WheelSpinService(api, session, new OverlayStateService(), new FixedRandom(0));

        var draw = await spins.DrawAsync(Streamer, new SpinnerConfig(), cancellationToken);

        Assert.Empty(draw.AvailableSongs);
        Assert.Null(draw.WinnerIndex);
        Assert.Null(draw.Winner);
    }

    [Fact]
    public async Task Given_DrawWithAWinner_When_Starting_Then_OverlayReceivesTheQueueThenTheSpinToTheWinner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42, 43)));
        var overlay = new OverlayStateService();
        await using var session = await StartSessionAsync(api, overlay, Streamer);
        var spins = new WheelSpinService(api, session, overlay, new FixedRandom(1));
        var draw = await spins.DrawAsync(Streamer, new SpinnerConfig(), cancellationToken);
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());

        spins.Start(draw);

        Assert.True(await events.MoveNextAsync());
        using var queue = OverlayStateServiceTests.ParseEventData(events.Current, OverlayEventNames.UpdateSongs);
        Assert.Equal(3, queue.RootElement.GetProperty("availableCount").GetInt32());
        Assert.True(await events.MoveNextAsync());
        using var spin = OverlayStateServiceTests.ParseEventData(events.Current, OverlayEventNames.SpinCommand);
        Assert.Equal("""{"winnerIndex":1,"winnerQueueId":42,"duration":5000}""", spin.RootElement.GetRawText());
    }

    [Fact]
    public async Task Given_DrawWithNoWinner_When_Starting_Then_OverlayReceivesTheEmptyQueueWithoutASpin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        var overlay = new OverlayStateService();
        await using var session = await StartSessionAsync(api, overlay, Streamer, Song(7));
        var spins = new WheelSpinService(api, session, overlay, new FixedRandom(0));
        var draw = await spins.DrawAsync(Streamer, new SpinnerConfig(), cancellationToken);
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());

        spins.Start(draw);
        // A later broadcast marks the end of what the start sent.
        overlay.BroadcastCloseWinner();

        Assert.True(await events.MoveNextAsync());
        using var queue = OverlayStateServiceTests.ParseEventData(events.Current, OverlayEventNames.UpdateSongs);
        Assert.Equal(0, queue.RootElement.GetProperty("availableCount").GetInt32());
        Assert.True(await events.MoveNextAsync());
        Assert.Equal(OverlayEventNames.CloseWinner, events.Current.Name);
    }

    [Fact]
    public async Task Given_SpinStarted_When_TheWheelHasNotStopped_Then_WinnerIsNotRevealed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        await using var spin = await DrawWinnerAsync(new ScriptedStreamerSongListClient(), time, showQueuePosition: false);

        var reveal = spin.Spins.RevealWinnerAsync(spin.Draw, cancellationToken);
        time.Advance(WheelSpinService.SpinDuration + WheelSpinService.WinnerRevealDelay - TimeSpan.FromTicks(1));

        Assert.False(reveal.IsCompleted);
    }

    [Fact]
    public async Task Given_SpinStarted_When_TheSpinAndRevealDelayPass_Then_WinnerIsRevealed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        await using var spin = await DrawWinnerAsync(new ScriptedStreamerSongListClient(), time, showQueuePosition: false);

        var reveal = spin.Spins.RevealWinnerAsync(spin.Draw, cancellationToken);
        time.Advance(WheelSpinService.SpinDuration + WheelSpinService.WinnerRevealDelay);
        var winner = await reveal.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(42, winner.Song.QueueId);
        Assert.Null(winner.QueuePosition);
    }

    [Fact]
    public async Task Given_DialogShowsQueuePositions_When_Revealing_Then_WinnerCarriesItsCurrentPosition()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        await using var spin = await DrawWinnerAsync(api, time, showQueuePosition: true);
        api.QueueResponses.Enqueue(_ => Task.FromResult(new SpinnerQueueSnapshot { Items = [Song(42, position: 5)] }));

        var reveal = spin.Spins.RevealWinnerAsync(spin.Draw, cancellationToken);
        time.Advance(WheelSpinService.SpinDuration + WheelSpinService.WinnerRevealDelay);
        var winner = await reveal.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(5, winner.QueuePosition);
    }

    [Fact]
    public async Task Given_QueuePositionLookupHangs_When_TheLookupTimesOut_Then_WinnerIsRevealedWithoutAPosition()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        await using var spin = await DrawWinnerAsync(api, time, showQueuePosition: true);
        api.QueueResponses.Enqueue(lookupToken => HangUntilCancelledAsync(lookupToken));

        var reveal = spin.Spins.RevealWinnerAsync(spin.Draw, cancellationToken);
        var revealDelay = await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken);
        time.Advance(revealDelay);
        var lookupTimeout = await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken);
        time.Advance(lookupTimeout);
        var winner = await reveal.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(WheelSpinService.QueuePositionLookupTimeout, lookupTimeout);
        Assert.Null(winner.QueuePosition);
    }

    [Fact]
    public async Task Given_AnotherChannelLoadedDuringTheSpin_When_Revealing_Then_WinnerIsShownWithoutAPosition()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        await using var spin = await DrawWinnerAsync(api, time, showQueuePosition: true);
        await spin.Session.StartAsync(2, "another-channel", new SpinnerConfig(), [], [], null, cancellationToken);

        var reveal = spin.Spins.RevealWinnerAsync(spin.Draw, cancellationToken);
        time.Advance(WheelSpinService.SpinDuration + WheelSpinService.WinnerRevealDelay);
        var winner = await reveal.WaitAsync(WaitLimit, cancellationToken);

        Assert.Null(winner.QueuePosition);
        Assert.Equal(1, api.QueueFetches);
    }

    [Fact]
    public async Task Given_SpinDrawn_When_LessThanTheCooldownHasPassed_Then_SpinsAreCoolingDown()
    {
        var time = new FakeTimeProvider();
        await using var spin = await DrawWinnerAsync(new ScriptedStreamerSongListClient(), time, showQueuePosition: false);

        time.Advance(WheelSpinService.Cooldown - TimeSpan.FromTicks(1));

        Assert.True(spin.Spins.IsCoolingDown);
    }

    [Fact]
    public async Task Given_SpinDrawn_When_TheCooldownHasPassed_Then_SpinsAreNoLongerCoolingDown()
    {
        var time = new FakeTimeProvider();
        await using var spin = await DrawWinnerAsync(new ScriptedStreamerSongListClient(), time, showQueuePosition: false);

        time.Advance(WheelSpinService.Cooldown);

        Assert.False(spin.Spins.IsCoolingDown);
    }

    [Fact]
    public async Task Given_SpinDrawn_When_TheSessionRefreshes_Then_TheRefreshIsSkipped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var spin = await DrawWinnerAsync(api, new FakeTimeProvider(), showQueuePosition: false);

        var refreshed = await spin.Session.RefreshAsync(Streamer, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal(1, api.QueueFetches);
    }

    [Fact]
    public async Task Given_TheQueueChangedDuringASpin_When_TheSpinFinishes_Then_TheChangedQueueIsPublishedWithoutAManualRefresh()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new TimerTrackingTimeProvider();
        var api = new ScriptedStreamerSongListClient();
        var events = new ChannelEventSource();
        var overlay = new OverlayStateService();
        await using var session = await StartSessionAsync(api, overlay, Streamer, time, events);
        var spins = new WheelSpinService(api, session, overlay, new FixedRandom(1), time);
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42)));
        await spins.DrawAsync(Streamer, new SpinnerConfig(), cancellationToken);
        events.Publish(StreamerSongListEventKind.QueueChanged);
        // The debounced refresh runs while the spin holds refreshes, so it is skipped.
        time.Advance(await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken));
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42, 43)));
        var published = new TaskCompletionSource<StreamerSessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, change) =>
        {
            if (change.Snapshot.AvailableSongs.Length == 3) published.TrySetResult(change.Snapshot);
        };

        spins.Finish();
        var snapshot = await AdvanceTimersUntilAsync(time, published.Task, cancellationToken);

        Assert.Equal([41, 42, 43], snapshot.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(2, api.QueueFetches);
    }

    [Fact]
    public async Task Given_ExcludePlayedSongsOn_When_Drawing_Then_PlayedSongsCannotWinAndHistoryCoversTheConfiguredPeriod()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        api.QueueResponses.Enqueue(_ => Task.FromResult(new SpinnerQueueSnapshot
        {
            Items =
            [
                new SpinnerQueueItem { QueueId = 41, Song = new SpinnerSong { Id = 500 } },
                new SpinnerQueueItem { QueueId = 42, Song = new SpinnerSong { Id = 501 } }
            ]
        }));
        api.PlayHistory = [new PlayHistoryItem { Song = new SpinnerSong { Id = 500 } }];
        await using var session = await StartSessionAsync(api, new OverlayStateService(), Streamer);
        // Slot 0 of the whole queue is the played song.
        var spins = new WheelSpinService(api, session, new OverlayStateService(), new FixedRandom(0));
        var config = new SpinnerConfig
        {
            PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true, Period = "month" }
        };

        var draw = await spins.DrawAsync(Streamer, config, cancellationToken);

        Assert.Equal([42], draw.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(42, draw.Winner?.QueueId);
        Assert.Equal(["month"], api.PlayHistoryPeriods);
    }

    private static async Task<StreamerSessionService> StartSessionAsync(
        ScriptedStreamerSongListClient api,
        OverlayStateService overlay,
        string streamer,
        params SpinnerQueueItem[] availableSongs) =>
        await StartSessionAsync(api, overlay, streamer, TimeProvider.System, new ChannelEventSource(), availableSongs);

    private static async Task<StreamerSessionService> StartSessionAsync(
        ScriptedStreamerSongListClient api,
        OverlayStateService overlay,
        string streamer,
        TimeProvider time,
        ChannelEventSource events,
        params SpinnerQueueItem[] availableSongs)
    {
        var session = new StreamerSessionService(api, events, overlay, time);
        await session.StartAsync(
            1,
            streamer,
            new SpinnerConfig(),
            availableSongs,
            [],
            null,
            TestContext.Current.CancellationToken);
        return session;
    }

    /// <summary>Loads <see cref="Streamer"/> and draws queue entry 42 from a queue of 41 and 42.</summary>
    private static async Task<SpinScenario> DrawWinnerAsync(
        ScriptedStreamerSongListClient api,
        TimeProvider time,
        bool showQueuePosition)
    {
        var overlay = new OverlayStateService();
        var session = await StartSessionAsync(api, overlay, Streamer);
        var spins = new WheelSpinService(api, session, overlay, new FixedRandom(1), time);
        var config = new SpinnerConfig
        {
            WinnerDialog = new SpinnerWinnerDialogConfig { ShowQueuePosition = showQueuePosition }
        };
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(41, 42)));
        var draw = await spins.DrawAsync(Streamer, config, TestContext.Current.CancellationToken);
        return new SpinScenario(session, spins, draw);
    }

    /// <summary>
    /// Moves the clock past each delay the session starts until <paramref name="outcome"/> completes. Whether
    /// resuming needs its own debounce depends on whether the skipped refresh had finished, so the test does not
    /// assume a number of delays.
    /// </summary>
    private static async Task<T> AdvanceTimersUntilAsync<T>(
        TimerTrackingTimeProvider time,
        Task<T> outcome,
        CancellationToken cancellationToken)
    {
        while (!outcome.IsCompleted)
        {
            var timer = time.WaitForTimerAsync(cancellationToken).AsTask();
            if (await Task.WhenAny(outcome, timer).WaitAsync(WaitLimit, cancellationToken) == timer)
                time.Advance(await timer);
        }

        return await outcome;
    }

    private static async Task<SpinnerQueueSnapshot> HangUntilCancelledAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new SpinnerQueueSnapshot();
    }

    private sealed record SpinScenario(StreamerSessionService Session, WheelSpinService Spins, SpinDraw Draw)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }

    /// <summary>Always picks the same wheel slot, so a test knows which song wins.</summary>
    private sealed class FixedRandom(int pick) : Random
    {
        public override int Next(int maxValue) => pick;
    }
}
