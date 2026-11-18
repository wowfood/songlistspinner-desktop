using System.Net;
using System.Text.RegularExpressions;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// How the Dashboard reports a StreamerSongList failure while loading a channel (a rejected token, rate limiting, a
/// dropped connection, the wrong platform), and a manual refresh. ChannelLoadingTests covers a server error and an
/// unknown channel.
/// </summary>
public class ApiErrorStateTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheApiRejectsTheToken_When_LoadingAChannel_Then_TheDashboardReportsTheRejectedToken()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("token_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        scenario.Simulator.FailNextRequests(HttpMethod.Get, "/streamers", HttpStatusCode.Unauthorized, "invalid access token");
        var dashboard = scenario.Dashboard;

        await dashboard.StartLoadingChannelAsync("token_streamer");

        const string apiError = "StreamerSongList rejected the configured API token. invalid access token";
        await Expect(dashboard.Status).ToHaveTextAsync($"Error: {apiError}");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await dashboard.Health.ExpectApiDetailAsync(apiError);
        var lookup = Assert.Single(scenario.Simulator.Requests);
        Assert.True(ApiCalls.ResolveStreamer("token_streamer")(lookup));
        Assert.Equal(401, lookup.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheApiRateLimitsTheQueue_When_LoadingAChannel_Then_TheDashboardAsksToTryAgainShortly()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("busy_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        scenario.Simulator.FailNextRequests(HttpMethod.Get, "/queue", HttpStatusCode.TooManyRequests, "slow down");
        var dashboard = scenario.Dashboard;

        await dashboard.StartLoadingChannelAsync("busy_streamer");

        const string apiError = "StreamerSongList rate-limited the request. Try again shortly. slow down";
        await Expect(dashboard.Status).ToHaveTextAsync($"Error: {apiError}");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await dashboard.Health.ExpectApiDetailAsync(apiError);
        var queueCall = Assert.Single(scenario.Simulator.RequestsTo(HttpMethod.Get, "/queue"));
        Assert.True(ApiCalls.FetchQueue("busy_streamer")(queueCall));
        Assert.Equal(429, queueCall.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheQueueConnectionDrops_When_LoadingAChannel_Then_TheDashboardReportsAnErrorAndOffersTheInputAgain()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("dropped_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        scenario.Simulator.DropNextRequests(HttpMethod.Get, "/queue");
        var dashboard = scenario.Dashboard;

        await dashboard.StartLoadingChannelAsync("dropped_streamer");

        // The transport's message depends on the machine, so only the prefix is fixed.
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await Expect(dashboard.Status).ToHaveTextAsync(new Regex("^Error: ."));
        await Expect(dashboard.StreamerInputError).ToHaveTextAsync(
            "⚠ Could not find or load streamer \"dropped_streamer\". Check the name and platform, then try again.");
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("Not loaded");
        var queueCall = Assert.Single(scenario.Simulator.RequestsTo(HttpMethod.Get, "/queue"));
        Assert.True(queueCall.ConnectionAborted);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AYouTubeChannel_When_LoadingItsNameOnTwitch_Then_TheDashboardReportsItNotFound()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("tube_streamer", "youtube").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        // The platform setting defaults to Twitch.
        await dashboard.StartLoadingChannelAsync("tube_streamer");

        await Expect(dashboard.Status).ToHaveTextAsync(
            "Error: StreamerSongList returned HTTP 404 (Not Found). streamer not found");
        var lookup = Assert.Single(scenario.Simulator.Requests);
        Assert.True(ApiCalls.ResolveStreamer("tube_streamer", "twitch")(lookup));
        Assert.Equal(404, lookup.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannelThatGainedARequest_When_PressingRefresh_Then_TheChannelIsLookedUpAgainAndTheStatusCountsTheNewQueue()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("refresh_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("refresh_streamer");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 2 songs. Press SPIN!");
        // Connecting to realtime reads the queue and history once more; wait for that, so the log is quiet.
        await WaitForQueueAndHistoryReadsAsync(scenario, "refresh_streamer", 2, cancellationToken);
        // The new request reaches the app over realtime, whose refresh redraws the wheel but leaves the status: only a
        // load or Refresh sets it. Wait for that refresh's reads too, so Refresh's calls come next in the log.
        var africa = SongCatalog.Africa;
        await channel.Channel.RequestSongAsync(africa.Artist, africa.Title, africa.Requester, africa.Donation);
        await dashboard.ExpectWheelLabelsAsync(
            SongCatalog.TakeOnMe.WheelLabel, SongCatalog.MrBrightside.WheelLabel, africa.WheelLabel);
        await WaitForQueueAndHistoryReadsAsync(scenario, "refresh_streamer", 3, cancellationToken);
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 2 songs. Press SPIN!");
        var callsBefore = scenario.Simulator.Requests.ToHashSet(ReferenceEqualityComparer.Instance);

        await dashboard.RefreshButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 3 songs. Press SPIN!");
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: refresh_streamer");
        await dashboard.ExpectWheelLabelsAsync(
            SongCatalog.TakeOnMe.WheelLabel, SongCatalog.MrBrightside.WheelLabel, africa.WheelLabel);
        // Refresh reloads the channel: a lookup, then its queue and history in either order.
        var refreshCalls = await scenario.Simulator.WaitForRequestCountAsync(
            request => !callsBefore.Contains(request), 3, cancellationToken);
        Assert.True(ApiCalls.ResolveStreamer("refresh_streamer")(refreshCalls[0]));
        Assert.Equal(
            ["/play_history", "/queue"],
            refreshCalls.Skip(1).Select(request => request.Path).Order(StringComparer.Ordinal));
    }

    private static async Task WaitForQueueAndHistoryReadsAsync(
        AppScenario scenario,
        string channel,
        int count,
        CancellationToken cancellationToken)
    {
        await scenario.Simulator.WaitForRequestCountAsync(ApiCalls.FetchQueue(channel), count, cancellationToken);
        await scenario.Simulator.WaitForRequestCountAsync(ApiCalls.FetchPlayHistory(channel), count, cancellationToken);
    }
}
