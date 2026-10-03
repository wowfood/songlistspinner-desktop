using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The overlay's played-songs panel and Now Playing panel as an OBS browser source renders them under saved
/// Overlay Layout and Spinner settings.
/// </summary>
public class OverlayPanelTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_SequenceNumbersSaved_When_TheOverlayShowsTheChannel_Then_ItNumbersThePlayedLinesLikeTheDashboard()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();
        await settings.SaveAsync();
        await SeedPlayedChannelAsync(scenario);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("panel_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        // Numbering starts at the bottom by default, so the newest song has the highest number.
        string[] expected = ["2. Artist: Daft Punk | Title: Get Lucky", "1. Artist: Queen | Title: Don't Stop Me Now"];
        await overlay.PlayedList.ExpectLinesAsync(expected);
        await dashboard.PlayedList.ExpectLinesAsync(expected);
        Assert.Equal(expected, await overlay.PlayedList.ReadLinesAsync());
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_HeaderRowAndNumbersSaved_When_TheOverlayShowsTheChannel_Then_ItRendersTheDashboardsTable()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();
        await settings.OverlayLayout.PlayedShowHeaders.CheckAsync();
        await settings.SaveAsync();
        await SeedPlayedChannelAsync(scenario);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("panel_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        string[][] rows = [["2.", "Daft Punk", "Get Lucky"], ["1.", "Queen", "Don't Stop Me Now"]];
        await overlay.PlayedList.ExpectHeadersAsync("#", "Artist", "Title");
        await overlay.PlayedList.ExpectTableRowsAsync(rows);
        await overlay.PlayedList.ExpectTableSeparatorAsync(" | ");
        await dashboard.PlayedList.ExpectHeadersAsync("#", "Artist", "Title");
        await dashboard.PlayedList.ExpectTableRowsAsync(rows);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ANowPlayingSongWithTheWorkflowOff_When_TheOverlayShowsTheChannel_Then_ItHidesTheNowPlayingPanel()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("panel_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("panel_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        // The label comes from the same state update that decides the panel, so the panel is settled once it shows.
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("panel_streamer");
        await Expect(overlay.NowPlaying).ToBeHiddenAsync();
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ANowPlayingSongWithTheWorkflowOn_When_TheOverlayShowsTheChannel_Then_ItShowsTheSongBottomLeft()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveNowPlayingWorkflowAsync(scenario.Settings, _ => Task.CompletedTask);
        await SeedNowPlayingChannelAsync(scenario);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("panel_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        await Expect(overlay.NowPlaying).ToBeVisibleAsync();
        await Expect(overlay.NowPlayingText).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
        await Expect(overlay.NowPlaying).ToHaveAttributeAsync("data-position", "bottom-left");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NowPlayingPositionWidthAndFontSizeSaved_When_TheOverlayShowsTheChannel_Then_ThePanelUsesThem()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveNowPlayingWorkflowAsync(scenario.Settings, async settings =>
        {
            var layout = settings.OverlayLayout;
            await layout.OpenAsync();
            await layout.NowPlayingPosition.SelectOptionAsync("top-right");
            await layout.NowPlayingWidth.FillAsync("32rem");
            await layout.NowPlayingWidth.BlurAsync();
            await layout.NowPlayingFontSize.FillAsync("1.5rem");
            await layout.NowPlayingFontSize.BlurAsync();
        });
        await SeedNowPlayingChannelAsync(scenario);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("panel_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        await Expect(overlay.NowPlayingText).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
        await Expect(overlay.NowPlaying).ToHaveAttributeAsync("data-position", "top-right");
        await OverlayPage.ExpectInlineStyleAsync(overlay.NowPlaying, "width", "32rem");
        await OverlayPage.ExpectInlineStyleAsync(overlay.NowPlayingText, "font-size", "1.5rem");
    }

    private static Task SeedPlayedChannelAsync(AppScenario scenario) =>
        new ChannelSeed("panel_streamer")
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);

    private static Task SeedNowPlayingChannelAsync(AppScenario scenario) =>
        new ChannelSeed("panel_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);

    /// <summary>Turns the Now Playing workflow on, applies <paramref name="moreChanges"/>, and saves.</summary>
    private static async Task SaveNowPlayingWorkflowAsync(SettingsPage settings, Func<SettingsPage, Task> moreChanges)
    {
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await moreChanges(settings);
        await settings.SaveAsync();
    }
}
