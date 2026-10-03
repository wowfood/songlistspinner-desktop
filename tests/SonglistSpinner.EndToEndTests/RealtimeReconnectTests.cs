using System.Net;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using SonglistSpinner.Simulator;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The loaded channel's realtime connection: losing it, reconnecting (after 1 s, doubling to 30 s), catching up on
/// what changed meanwhile, and retrying a queue refresh an event triggered when it fails.
/// </summary>
public class RealtimeReconnectTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string ApiFailure = "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable";

    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannel_When_TheRealtimeConnectionDropsAndCannotReconnect_Then_TheDashboardReportsReconnecting()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = await LoadConnectedChannelAsync(scenario);

        scenario.Simulator.RejectEventConnections = true;
        scenario.Simulator.DropEventConnections();

        await Expect(dashboard.Health.Realtime).ToHaveTextAsync("Reconnecting");
        await Expect(dashboard.Status).ToHaveTextAsync("Realtime updates disconnected; reconnecting...");
        // The queue itself is still served, so only realtime is degraded.
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Connected");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ARealtimeConnectionThatIsReconnecting_When_TheServiceAcceptsConnectionsAgain_Then_TheDashboardReportsItReconnected()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = await LoadConnectedChannelAsync(scenario);
        await DisconnectRealtimeAsync(scenario, dashboard);

        // Cleared at once, so the next attempt is a few seconds away at most.
        scenario.Simulator.RejectEventConnections = false;

        await Expect(dashboard.Health.Realtime).ToHaveTextAsync("Connected");
        await dashboard.Health.ExpectRealtimeDetailAsync("Receiving live queue and history events.");
        await Expect(dashboard.Status).ToHaveTextAsync("Realtime updates reconnected.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASongRequestedWhileRealtimeWasDown_When_RealtimeReconnects_Then_TheWheelCatchesUpWithIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("realtime_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var dashboard = await LoadConnectedChannelAsync(scenario, channel);
        await DisconnectRealtimeAsync(scenario, dashboard);
        // No socket is subscribed, so this request's event reaches nobody; only a fetch on reconnecting finds it.
        await channel.Channel.RequestSongAsync("Rick Astley", "Never Gonna Give You Up", "late_viewer");

        scenario.Simulator.RejectEventConnections = false;

        await Expect(dashboard.Status).ToHaveTextAsync("Realtime updates reconnected.");
        await dashboard.ExpectWheelLabelsAsync(
            SongCatalog.TakeOnMe.WheelLabel,
            SongCatalog.MrBrightside.WheelLabel,
            "Rick Astley - Never Gonna Give You Up (late_viewer)");
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("3");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnEventTriggeredRefreshFails_When_ItsRetryIsPending_Then_TheDashboardReportsTheRefreshFailure()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("refresh_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var dashboard = await LoadConnectedChannelAsync(scenario, channel);
        var (failingFetch, retry) = ArmFailingRefresh(scenario.Simulator);

        await channel.Channel.RequestSongAsync("Rick Astley", "Never Gonna Give You Up", "late_viewer");
        await failingFetch.Arrived.WaitAsync(cancellationToken);
        failingFetch.Release();
        // The retry, a second after the failure, is held, so the failure stays on screen.
        await retry.Arrived.WaitAsync(cancellationToken);

        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await dashboard.Health.ExpectApiDetailAsync(ApiFailure);
        await Expect(dashboard.Status).ToHaveTextAsync("Realtime refresh failed.");
        await dashboard.ExpectWheelLabelsAsync(SongCatalog.TakeOnMe.WheelLabel, SongCatalog.MrBrightside.WheelLabel);
        // The load's fetch, the fetch on connecting, then the event's failed one; the retry is still unanswered.
        Assert.Equal(
            [200, 200, 500],
            scenario.Simulator.RequestsTo(HttpMethod.Get, "/queue").Select(request => request.StatusCode));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AFailedEventTriggeredRefresh_When_ItsRetrySucceeds_Then_TheDashboardRecoversWithTheNewSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("refresh_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var dashboard = await LoadConnectedChannelAsync(scenario, channel);
        var (failingFetch, retry) = ArmFailingRefresh(scenario.Simulator);
        await channel.Channel.RequestSongAsync("Rick Astley", "Never Gonna Give You Up", "late_viewer");
        await failingFetch.Arrived.WaitAsync(cancellationToken);
        failingFetch.Release();
        await retry.Arrived.WaitAsync(cancellationToken);
        await Expect(dashboard.Status).ToHaveTextAsync("Realtime refresh failed.");

        retry.Release();

        await Expect(dashboard.Status).ToHaveTextAsync("Realtime refresh recovered.");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Connected");
        await dashboard.ExpectWheelLabelsAsync(
            SongCatalog.TakeOnMe.WheelLabel,
            SongCatalog.MrBrightside.WheelLabel,
            "Rick Astley - Never Gonna Give You Up (late_viewer)");
        Assert.Equal(
            [500, 200],
            (await scenario.Simulator.WaitForRequestCountAsync(ApiCalls.FetchQueue("refresh_streamer"), 4, cancellationToken))
            .Skip(2).Select(request => request.StatusCode));
    }

    /// <summary>
    /// Loads a channel and waits until its realtime connection is up and the queue fetch that connecting triggers
    /// has been answered, so faults a test arms afterwards meet only the calls the test causes.
    /// </summary>
    private static async Task<DashboardPage> LoadConnectedChannelAsync(AppScenario scenario, SeededChannel? channel = null)
    {
        channel ??= await new ChannelSeed("reconnect_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync(channel.Name);
        await Expect(dashboard.Health.Realtime).ToHaveTextAsync("Connected");
        await scenario.Simulator.WaitForRequestCountAsync(
            ApiCalls.FetchQueue(channel.Name),
            2,
            TestContext.Current.CancellationToken);
        return dashboard;
    }

    private static async Task DisconnectRealtimeAsync(AppScenario scenario, DashboardPage dashboard)
    {
        scenario.Simulator.RejectEventConnections = true;
        scenario.Simulator.DropEventConnections();
        await Expect(dashboard.Health.Realtime).ToHaveTextAsync("Reconnecting");
    }

    /// <summary>
    /// The next queue fetch is held, then fails with a 500; the one after it (the retry) is held until released.
    /// A hold applies before an injected failure, and holds are taken in the order they were armed.
    /// </summary>
    private static (RequestHold FailingFetch, RequestHold Retry) ArmFailingRefresh(StreamerSongListSimulator simulator)
    {
        var failingFetch = simulator.HoldNextRequest(HttpMethod.Get, "/queue");
        var retry = simulator.HoldNextRequest(HttpMethod.Get, "/queue");
        simulator.FailNextRequests(HttpMethod.Get, "/queue", HttpStatusCode.InternalServerError, "database unavailable");
        return (failingFetch, retry);
    }
}
