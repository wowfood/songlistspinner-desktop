using System.Text.RegularExpressions;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The played-songs panel's layout on the Dashboard: its side, text size, line clamp and background opacity, as
/// set in Settings &gt; Overlay Layout and Settings &gt; Appearance.
/// </summary>
public class PlayedListLayoutTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string PlayedListLeftClass = "played-list-left";

    [Fact(Timeout = 180_000)]
    public async Task Given_AMaximumOfThreeLinesPerSong_When_TheDashboardOpens_Then_ThePanelClampsSongsToThreeLines()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await StartWithSettingsAsync(new { PlayedListMaxLines = 3 }, cancellationToken);

        await scenario.Dashboard.ExpectThemeVariableAsync("--app-played-list-max-lines", "3");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDashboard_When_APlayedListFontSizeIsSavedInSettings_Then_ThePanelUsesThatFontSize()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedFontSize.FillAsync("1.25rem");
        // The field saves its value on change, which a text input raises when it loses focus.
        await settings.OverlayLayout.PlayedFontSize.BlurAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");

        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.ExpectThemeVariableAsync("--app-played-list-font-size", "1.25rem");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDefaultLayout_When_TheDashboardOpens_Then_ThePanelSitsOnTheRightWithItsArrowPointingRight()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        var scenario = await sharedApp.BeginTestAsync(cancellationToken);

        var dashboard = scenario.Dashboard;
        await Expect(dashboard.PlayedList.Root).ToHaveAttributeAsync("data-position", "right");
        await Expect(dashboard.CollapseIcon).ToHaveTextAsync("▶");
        await Expect(dashboard.Container).Not.ToHaveClassAsync(new Regex($@"\b{PlayedListLeftClass}\b"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDashboard_When_TheLeftPanelPositionIsSavedInSettings_Then_ThePanelSitsOnTheLeftWithItsArrowPointingLeft()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedPosition.SelectOptionAsync("left");

        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await Expect(dashboard.PlayedList.Root).ToHaveAttributeAsync("data-position", "left");
        await Expect(dashboard.CollapseIcon).ToHaveTextAsync("◀");
        await Expect(dashboard.Container).ToHaveClassAsync(new Regex($@"\b{PlayedListLeftClass}\b"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDefaultSeventyPercentPanel_When_HalfOpacityIsSavedInAppearance_Then_ThePanelBackgroundIsHalfTransparentBlack()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await scenario.Dashboard.ExpectThemeVariableAsync("--app-played-list-bg", "rgba(0, 0, 0, 0.7)");
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.PlayedPanelOpacity.FillAsync("50");
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");

        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.ExpectThemeVariableAsync("--app-played-list-bg", "rgba(0,0,0,0.50)");
    }

    private static async Task<DashboardPage> SaveAndOpenDashboardAsync(AppScenario scenario)
    {
        await scenario.Settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        return dashboard;
    }

    private static Task<AppScenario> StartWithSettingsAsync(object settings, CancellationToken cancellationToken) =>
        AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(settings);
            return Task.CompletedTask;
        });
}
