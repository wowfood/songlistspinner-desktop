using System.Net;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>The Now Playing panel's Mark Played, which completes the playing song without a spin.</summary>
public class NowPlayingCompletionTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASongIsPlaying_When_ThePanelsMarkPlayedIsPressed_Then_ItIsPostedAndMovesToThePlayedList()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("playing_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("playing_streamer", scenario.Simulator, cancellationToken);
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");

        await dashboard.MarkNowPlayingPlayedAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Now Playing marked as played.");
        await Expect(dashboard.NowPlayingControl).ToHaveCountAsync(0);
        await dashboard.PlayedList.ExpectLinesAsync("Artist: Fleetwood Mac | Title: Dreams");
        var markPlayed = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.MarkNowPlayingPlayed(channel.StreamerId),
            cancellationToken);
        Assert.Equal(204, markPlayed.StatusCode);
        Assert.Null(channel.Channel.NowPlaying);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_MarkPlayedFailsOnTheServer_When_ThePanelsMarkPlayedIsPressed_Then_TheStatusShowsTheReasonAndTheSongStaysPlaying()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("failed_playing_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("failed_playing_streamer", scenario.Simulator, cancellationToken);
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
        scenario.Simulator.FailNextRequests(
            HttpMethod.Post,
            "/queue/played",
            HttpStatusCode.InternalServerError,
            "database unavailable");

        await dashboard.MarkNowPlayingPlayedButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync(
            "StreamerSongList failed while marking Now Playing as played: " +
            "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
        await Expect(dashboard.MarkNowPlayingPlayedButton).ToHaveTextAsync("Mark Played");
        var markPlayed = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.MarkNowPlayingPlayed(channel.StreamerId),
            cancellationToken);
        Assert.Equal(500, markPlayed.StatusCode);
        Assert.Equal(channel.NowPlaying?.QueueId, channel.Channel.NowPlaying?.QueueId);
    }
}
