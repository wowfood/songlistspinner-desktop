using System.Net;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;
using SonglistSpinner.Simulator;
using SonglistSpinner.Testing;
using Xunit;
using static SonglistSpinner.IntegrationTests.SimulatorClients;

namespace SonglistSpinner.IntegrationTests.Services;

/// <remarks>
/// Loading a channel connects to the event service, and connecting requests a refresh, so the session's first
/// timer is always that refresh's debounce.
/// </remarks>
public class StreamerSessionServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ALoadedChannel_When_AViewerRequestsASong_Then_TheRealtimeRefreshAddsItToTheSnapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        var sessionTime = new TimerTrackingTimeProvider();
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel(Streamer);
        await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        await using var app = new SessionHarness(simulator, clock, sessionTime);
        await app.Loader.LoadAsync(Streamer, new SpinnerConfig(), cancellationToken);
        // Let the refresh that connecting requested finish reading the queue, so it cannot pick up the request.
        var connectedRefresh = simulator.WaitForRequestAsync(request => request.Path == "/queue", cancellationToken);
        await AdvancePastNextTimerAsync(sessionTime);
        await connectedRefresh.WaitAsync(WaitLimit, cancellationToken);
        var requestShown = WaitForSnapshot(app.Session, snapshot => snapshot.AvailableSongs.Length == 2);

        var requested = await channel.RequestSongAsync("a-ha", "Take On Me", "synth_lover");
        // Only the published queue_add can start this second debounce.
        await AdvancePastNextTimerAsync(sessionTime);

        var snapshot = await requestShown.WaitAsync(WaitLimit, cancellationToken);
        Assert.Equal(requested.QueueId, snapshot.AvailableSongs[^1].QueueId);
        Assert.Equal("Take On Me", snapshot.AvailableSongs[^1].Song.Title);
    }

    [Fact]
    public async Task Given_TheQueueFetchFailsOnceWithHttp500_When_TheRealtimeRefreshRuns_Then_ItReportsTheApiErrorAndRecoversOnRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        var sessionTime = new TimerTrackingTimeProvider();
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel(Streamer);
        await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        await using var app = new SessionHarness(simulator, clock, sessionTime);
        await app.Loader.LoadAsync(Streamer, new SpinnerConfig(), cancellationToken);
        simulator.FailNextRequests(HttpMethod.Get, "/queue", HttpStatusCode.InternalServerError, "database unavailable");
        var failed = WaitForSnapshot(app.Session, snapshot => snapshot.ApiHealth == StreamerSessionHealth.Failed);
        var recovered = new TaskCompletionSource<StreamerSessionChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Session.Changed += (_, change) =>
        {
            if (change.Announcement == "Realtime refresh recovered.") recovered.TrySetResult(change);
        };

        await AdvancePastNextTimerAsync(sessionTime);
        var failure = await failed.WaitAsync(WaitLimit, cancellationToken);
        var retryDelay = await AdvancePastNextTimerAsync(sessionTime);
        var recovery = await recovered.Task.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(
            "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable",
            failure.ApiHealthDetail);
        Assert.Equal(TimeSpan.FromSeconds(1), retryDelay);
        Assert.Equal(StreamerSessionHealth.Healthy, recovery.Snapshot.ApiHealth);
    }

    private static async Task<TimeSpan> AdvancePastNextTimerAsync(TimerTrackingTimeProvider time)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dueTime = await time.WaitForTimerAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken);
        time.Advance(dueTime);
        return dueTime;
    }

    private static Task<StreamerSessionSnapshot> WaitForSnapshot(
        StreamerSessionService session,
        Func<StreamerSessionSnapshot, bool> condition)
    {
        var reached = new TaskCompletionSource<StreamerSessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, change) =>
        {
            if (condition(change.Snapshot)) reached.TrySetResult(change.Snapshot);
        };
        return reached.Task;
    }
}
