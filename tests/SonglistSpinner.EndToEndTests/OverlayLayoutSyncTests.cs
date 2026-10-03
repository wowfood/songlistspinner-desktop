using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The Dashboard's played-list layout (collapsed, resized) mirrored by an open OBS browser source. Each test leaves
/// overlay layout a reset cannot undo, so the next test in the class relaunches the app.
/// </summary>
public class OverlayLayoutSyncTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_AnOpenOverlay_When_TheDashboardCollapsesThePlayedList_Then_TheOverlayHidesItsPlayedList()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var (dashboard, overlay) = await OpenLoadedOverlayAsync(scenario);

        await dashboard.CollapseButton.ClickAsync();

        await Expect(dashboard.CollapseButton).ToHaveAttributeAsync("aria-expanded", "false");
        await overlay.ExpectPlayedListCollapsedAsync();
        await Expect(overlay.PlayedList.Root).ToBeHiddenAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ACollapsedPlayedList_When_TheDashboardExpandsIt_Then_TheOverlayShowsItsPlayedListAgain()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var (dashboard, overlay) = await OpenLoadedOverlayAsync(scenario);
        await dashboard.CollapseButton.ClickAsync();
        await overlay.ExpectPlayedListCollapsedAsync();

        await dashboard.CollapseButton.ClickAsync();

        await Expect(dashboard.CollapseButton).ToHaveAttributeAsync("aria-expanded", "true");
        await overlay.ExpectPlayedListExpandedAsync();
        await overlay.PlayedList.ExpectLinesAsync("Artist: Daft Punk | Title: Get Lucky");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ACollapsedPlayedList_When_TheUserVisitsSettingsAndReturns_Then_TheOverlayMatchesTheExpandedDashboard()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var (dashboard, overlay) = await OpenLoadedOverlayAsync(scenario);
        await dashboard.CollapseButton.ClickAsync();
        await overlay.ExpectPlayedListCollapsedAsync();

        await scenario.Settings.OpenAsync();
        await dashboard.OpenAsync();

        // A new Dashboard starts with the list expanded; the browser source must not stay collapsed behind it.
        await Expect(dashboard.CollapseButton).ToHaveAttributeAsync("aria-expanded", "true");
        await overlay.ExpectPlayedListExpandedAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnOpenOverlay_When_TheDashboardPlayedListIsWidenedFromTheKeyboard_Then_TheOverlayTakesTheSameWidth()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var (dashboard, overlay) = await OpenLoadedOverlayAsync(scenario);
        var resizeHandle = dashboard.Page.GetByRole(
            Microsoft.Playwright.AriaRole.Separator,
            new() { Name = "Resize played songs panel", Exact = true });
        var initialWidth = await ReadPixelWidthAsync(dashboard);

        await resizeHandle.FocusAsync();
        await resizeHandle.PressAsync("ArrowLeft");
        await resizeHandle.PressAsync("ArrowLeft");

        // With the list on the right, each ArrowLeft widens it by 24 px from its current width. The list animates its
        // width, so the second step can start from part-way through the first; only "at least one step wider" is
        // fixed, and the overlay must then take whatever width the Dashboard settled on.
        var widenedWidth = double.Parse(
            await resizeHandle.GetAttributeAsync("aria-valuenow") ?? "",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(
            widenedWidth >= Math.Round(initialWidth + 24),
            $"The list widened from {initialWidth} px to only {widenedWidth} px.");
        var dashboardWidth = await dashboard.PlayedList.Root.EvaluateAsync<string>("list => list.style.width");
        var dashboardMinWidth = await dashboard.PlayedList.Root.EvaluateAsync<string>("list => list.style.minWidth");
        Assert.Matches(@"^\d+(\.\d+)?%$", dashboardWidth);
        Assert.Equal("300px", dashboardMinWidth);
        await OverlayPage.ExpectInlineStyleAsync(overlay.PlayedList.Root, "width", dashboardWidth);
        await OverlayPage.ExpectInlineStyleAsync(overlay.PlayedList.Root, "min-width", dashboardMinWidth);
    }

    private static async Task<(DashboardPage Dashboard, OverlayPage Overlay)> OpenLoadedOverlayAsync(AppScenario scenario)
    {
        await new ChannelSeed("layout_streamer")
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("layout_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("layout_streamer");
        await Expect(dashboard.Health.Overlay).ToHaveTextAsync("1 connected");
        await overlay.ExpectPlayedListExpandedAsync();
        return (dashboard, overlay);
    }

    private static Task<double> ReadPixelWidthAsync(DashboardPage dashboard) =>
        dashboard.PlayedList.Root.EvaluateAsync<double>("list => list.getBoundingClientRect().width");
}
