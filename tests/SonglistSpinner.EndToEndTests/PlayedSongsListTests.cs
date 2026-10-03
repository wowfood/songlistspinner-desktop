using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class PlayedSongsListTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_PlaysSpreadOverTwoMonths_When_TheChannelLoadsWithDefaultSettings_Then_TheListShowsTheLastSevenDaysNewestFirst()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("history_streamer")
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .WithPlayed(SongCatalog.Africa, TimeSpan.FromDays(60))
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.DancingQueen, TimeSpan.FromDays(20))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync("history_streamer");

        // The default period is "Last 7 days"; the default fields are artist and title, labelled, joined by " | ".
        await dashboard.PlayedList.ExpectLinesAsync(
            "Artist: Daft Punk | Title: Get Lucky",
            "Artist: Queen | Title: Don't Stop Me Now");
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("2");
    }
}
