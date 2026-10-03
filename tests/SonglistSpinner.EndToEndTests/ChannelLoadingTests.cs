using System.Net;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class ChannelLoadingTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheQueueEndpointFails_When_LoadingAChannel_Then_TheDashboardReportsTheServerErrorAndOffersTheInputAgain()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("failing_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        scenario.Simulator.FailNextRequests(
            HttpMethod.Get,
            "/queue",
            HttpStatusCode.InternalServerError,
            "database unavailable");
        var dashboard = scenario.Dashboard;

        await dashboard.StartLoadingChannelAsync("failing_streamer");

        const string apiError = "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable";
        await Expect(dashboard.StreamerInputError).ToHaveTextAsync(
            "⚠ Could not find or load streamer \"failing_streamer\". Check the name and platform, then try again.");
        await Expect(dashboard.Status).ToHaveTextAsync($"Error: {apiError}");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await dashboard.Health.ExpectApiDetailAsync(apiError);
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("Not loaded");
        var queueCall = Assert.Single(scenario.Simulator.RequestsTo(HttpMethod.Get, "/queue"));
        Assert.Equal(500, queueCall.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NoSuchChannel_When_LoadingIt_Then_TheDashboardReportsItNotFoundWithoutFetchingAQueue()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("real_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.StartLoadingChannelAsync("missing_streamer");

        await Expect(dashboard.Status).ToHaveTextAsync(
            "Error: StreamerSongList returned HTTP 404 (Not Found). streamer not found");
        await Expect(dashboard.StreamerInputError).ToHaveTextAsync(
            "⚠ Could not find or load streamer \"missing_streamer\". Check the name and platform, then try again.");
        var resolve = Assert.Single(scenario.Simulator.Requests);
        Assert.True(ApiCalls.ResolveStreamer("missing_streamer")(resolve));
        Assert.Equal(404, resolve.StatusCode);
    }
}
