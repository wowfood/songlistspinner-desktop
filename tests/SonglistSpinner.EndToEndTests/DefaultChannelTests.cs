using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// A saved default channel, which the Dashboard loads by itself at startup. Each test saves its settings and seeds
/// the channel before its own app starts.
/// </summary>
public class DefaultChannelTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASavedDefaultChannel_When_TheAppStarts_Then_TheDashboardLoadsItOnceWithoutAChangeButton()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(cancellationToken, prepare: async (simulator, profile) =>
        {
            profile.SaveSettings(new { DefaultStreamerName = "default_streamer" });
            await new ChannelSeed("default_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(simulator);
        });

        var dashboard = scenario.Dashboard;
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: default_streamer");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 1 songs. Press SPIN!");
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("default_streamer");
        // Changing away from the default is hidden unless the user opts in (HideChangeOptionWhenDefault).
        await Expect(dashboard.ChangeButton).ToHaveCountAsync(0);
        // The realtime connection the load opens fetches the queue a second time; by then a second load would have
        // looked the channel up again.
        await scenario.Simulator.WaitForRequestCountAsync(ApiCalls.FetchQueue("default_streamer"), 2, cancellationToken);
        Assert.Single(scenario.Simulator.RequestsTo(HttpMethod.Get, "/streamers"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ADefaultChannelWithTheChangeOptionKept_When_TheAppStarts_Then_TheChannelCanBeChanged()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(cancellationToken, prepare: async (simulator, profile) =>
        {
            profile.SaveSettings(new { DefaultStreamerName = "default_streamer", HideChangeOptionWhenDefault = false });
            await new ChannelSeed("default_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(simulator);
        });

        var dashboard = scenario.Dashboard;
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: default_streamer");
        await Expect(dashboard.ChangeButton).ToBeVisibleAsync();
        await Expect(dashboard.ChangeButton).ToHaveTextAsync("Change");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASavedYouTubeDefaultChannel_When_TheAppStarts_Then_EveryCallNamesTheYouTubeIdentity()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(cancellationToken, prepare: async (simulator, profile) =>
        {
            profile.SaveSettings(new { DefaultStreamerName = "tube_streamer", StreamerPlatform = "youtube" });
            await new ChannelSeed("tube_streamer", "youtube")
                .WithQueued(SongCatalog.TakeOnMe, SongCatalog.Africa)
                .ApplyAsync(simulator);
        });

        var dashboard = scenario.Dashboard;
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: tube_streamer");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 2 songs. Press SPIN!");
        var simulator = scenario.Simulator;
        Assert.Equal(200, (await simulator.WaitForFirstRequestAsync(ApiCalls.ResolveStreamer("tube_streamer", "youtube"), cancellationToken)).StatusCode);
        Assert.Equal(200, (await simulator.WaitForFirstRequestAsync(ApiCalls.FetchQueue("tube_streamer", "youtube"), cancellationToken)).StatusCode);
        Assert.Equal(200, (await simulator.WaitForFirstRequestAsync(ApiCalls.FetchPlayHistory("tube_streamer", "youtube"), cancellationToken)).StatusCode);
        Assert.All(simulator.Requests, request => Assert.Equal("youtube", request.Query.GetValueOrDefault("platform")));
    }
}
