using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Settings &gt; Spinner &amp; Queue &gt; play history period: which plays the Dashboard's played list shows, and
/// whether the app asks StreamerSongList for history after a cutoff (<c>played_after</c>) or for all of it.
/// </summary>
public class PlayHistoryPeriodTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string Channel = "history_streamer";
    private const string PlayedAfter = "played_after";

    private const string GetLuckyLine = "Artist: Daft Punk | Title: Get Lucky";
    private const string DontStopMeNowLine = "Artist: Queen | Title: Don't Stop Me Now";
    private const string DancingQueenLine = "Artist: ABBA | Title: Dancing Queen";
    private const string AfricaLine = "Artist: Toto | Title: Africa";

    [Fact(Timeout = 180_000)]
    public async Task Given_PlaysSpreadOverTwoMonths_When_ThePeriodIsTheLast24Hours_Then_OnlyTheHourOldPlayIsListedAndHistoryIsFetchedAfterACutoff()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { PlayHistoryPeriod = "day" }, cancellationToken);
        await SeedPlaysSpreadOverTwoMonths().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(GetLuckyLine);
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("1");
        var history = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.FetchPlayHistory(Channel), cancellationToken);
        Assert.True(history.Query.ContainsKey(PlayedAfter), "The history request should carry played_after.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PlaysSpreadOverTwoMonths_When_LastMonthIsChosenInSettings_Then_ThePlaysOfTheLastMonthAreListedNewestFirst()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedPlaysSpreadOverTwoMonths().ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.SelectPlayHistoryPeriodAsync("month");
        await settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(GetLuckyLine, DontStopMeNowLine, DancingQueenLine);
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("3");
        var history = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.FetchPlayHistory(Channel), cancellationToken);
        Assert.True(history.Query.ContainsKey(PlayedAfter), "The history request should carry played_after.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PlaysSpreadOverTwoMonths_When_ThePeriodIsAllTime_Then_EveryPlayIsListedAndHistoryIsFetchedWithoutACutoff()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { PlayHistoryPeriod = "all" }, cancellationToken);
        await SeedPlaysSpreadOverTwoMonths().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(GetLuckyLine, DontStopMeNowLine, DancingQueenLine, AfricaLine);
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("4");
        var history = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.FetchPlayHistory(Channel), cancellationToken);
        Assert.False(history.Query.ContainsKey(PlayedAfter), "An all-time history request should not carry played_after.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PlaysSpreadOverTwoMonths_When_ThePeriodIsMostRecentPlays_Then_EveryPlayIsListedAndHistoryIsFetchedWithoutACutoff()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { PlayHistoryPeriod = "stream" }, cancellationToken);
        await SeedPlaysSpreadOverTwoMonths().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(GetLuckyLine, DontStopMeNowLine, DancingQueenLine, AfricaLine);
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("4");
        var history = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.FetchPlayHistory(Channel), cancellationToken);
        Assert.False(history.Query.ContainsKey(PlayedAfter), "A most-recent-plays request should not carry played_after.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheOnlyPlayWasThreeDaysAgo_When_ThePeriodIsTheLast24Hours_Then_TheListSaysThePeriodHasNoPlays()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { PlayHistoryPeriod = "day" }, cancellationToken);
        await new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await Expect(dashboard.PlayedList.EmptyState).ToHaveTextAsync("No played songs in the selected history period.");
        await Expect(dashboard.PlayedList.Items).ToHaveCountAsync(1);
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("0");
    }

    // Played an hour, three days, twenty days and sixty days ago: each period's edge is far from every play.
    private static ChannelSeed SeedPlaysSpreadOverTwoMonths() =>
        new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .WithPlayed(SongCatalog.DancingQueen, TimeSpan.FromDays(20))
            .WithPlayed(SongCatalog.Africa, TimeSpan.FromDays(60))
            .WithQueued(SongCatalog.TakeOnMe);

    private static Task<AppScenario> StartWithSettingsAsync(object settings, CancellationToken cancellationToken) =>
        AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(settings);
            return Task.CompletedTask;
        });
}
