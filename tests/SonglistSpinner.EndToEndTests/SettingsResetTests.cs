using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Reverting Settings to the shipped defaults: each subsection's "Revert to default" and Advanced's reset of all
/// settings review the fields that differ before they change the draft, and only Save Settings applies it.
/// </summary>
public class SettingsResetTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    // SpinnerDefaults.CreateWheelColors, one colour per line as the Wheel Colors box shows them.
    private const string DefaultWheelColors = "#ff6b6b\n#4ecdc4\n#45b7d1\n#f9ca24\n#6c5ce7\n#a29bfe\n#fd79a8\n#fdcb6e";

    [Fact(Timeout = 180_000)]
    public async Task Given_PlayedPanelLabelsAndNumbersChanged_When_ThePanelResetIsConfirmed_Then_BothReturnToDefaults()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var layout = settings.OverlayLayout;
        await settings.OpenAsync();
        await layout.OpenAsync();
        await layout.PlayedShowLabels.UncheckAsync();
        await layout.PlayedShowNumbers.CheckAsync();

        await layout.RevertButton("Played Songs Panel").ClickAsync();
        var review = settings.MessageBox("Review Played Songs panel reset");
        await review.ExpectOpenAsync();
        await Expect(review.ResetFields).ToHaveTextAsync(["Field labels", "Sequence numbers"]);
        await review.ChooseAsync("Reset draft to defaults");

        await Expect(layout.PlayedShowLabels).ToBeCheckedAsync();
        await Expect(layout.PlayedShowNumbers).Not.ToBeCheckedAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PlayedPanelLabelsAndNumbersChanged_When_ThePanelResetIsDeclined_Then_TheChangesStay()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var layout = settings.OverlayLayout;
        await settings.OpenAsync();
        await layout.OpenAsync();
        await layout.PlayedShowLabels.UncheckAsync();
        await layout.PlayedShowNumbers.CheckAsync();

        await layout.RevertButton("Played Songs Panel").ClickAsync();
        var review = settings.MessageBox("Review Played Songs panel reset");
        await review.ExpectOpenAsync();
        await review.ChooseAsync("Keep current settings");

        await Expect(layout.PlayedShowLabels).Not.ToBeCheckedAsync();
        await Expect(layout.PlayedShowNumbers).ToBeCheckedAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheWinnerDialogAtDefaults_When_ItsRevertIsPressed_Then_ANoticeSaysItAlreadyUsesDefaults()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();

        await settings.OverlayLayout.RevertButton("Winner Dialog Settings").ClickAsync();

        var notice = settings.MessageBox("Winner dialog already uses defaults");
        await notice.ExpectOpenAsync();
        await Expect(notice.Buttons).ToHaveTextAsync(["OK"]);
        await notice.ChooseAsync("OK");
        await Expect(settings.MessageBox("Review Winner dialog reset").Root).ToHaveCountAsync(0);
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NowPlayingLabelsTurnedOff_When_TheNowPlayingPanelResetIsConfirmed_Then_LabelsReturnAndTheWorkflowStaysOn()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var layout = settings.OverlayLayout;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await layout.OpenAsync();
        await layout.NowPlayingShowLabels.UncheckAsync();

        await layout.RevertButton("Now Playing Panel").ClickAsync();
        var review = settings.MessageBox("Review Now Playing panel reset");
        await review.ExpectOpenAsync();
        // The workflow switch belongs to Spinner & Queue, so the panel's reset leaves it out.
        await Expect(review.ResetFields).ToHaveTextAsync(["Field labels"]);
        await review.ChooseAsync("Reset draft to defaults");

        await Expect(layout.NowPlayingShowLabels).ToBeCheckedAsync();
        await settings.Spinner.OpenAsync();
        await Expect(settings.Spinner.NowPlayingWorkflow).ToBeCheckedAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ATransparentBackground_When_TheBackgroundResetIsConfirmed_Then_TheModeReturnsToSolidColor()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var appearance = settings.Appearance;
        await settings.OpenAsync();
        await appearance.OpenAsync();
        await appearance.BackgroundMode.SelectOptionAsync("transparent");

        await appearance.RevertButton("Background").ClickAsync();
        var review = settings.MessageBox("Review Background reset");
        await review.ExpectOpenAsync();
        await Expect(review.ResetFields).ToHaveTextAsync(["Mode"]);
        await review.ChooseAsync("Reset draft to defaults");

        await Expect(appearance.BackgroundMode).ToHaveValueAsync("color");
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_EditedWheelColors_When_TheWheelPaletteResetIsConfirmed_Then_TheShippedPaletteReturns()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var appearance = settings.Appearance;
        await settings.OpenAsync();
        await appearance.OpenAsync();
        await Expect(appearance.WheelColors).ToHaveValueAsync(DefaultWheelColors);
        await appearance.WheelColors.FillAsync("#000000\n#ffffff");
        // The box binds on change, so leave it before reviewing.
        await appearance.WheelColors.BlurAsync();

        await appearance.RevertButton("Wheel Palette").ClickAsync();
        var review = settings.MessageBox("Review Wheel palette reset");
        await review.ExpectOpenAsync();
        await Expect(review.ResetFields).ToHaveTextAsync(["Wheel colors"]);
        await review.ChooseAsync("Reset draft to defaults");

        await Expect(appearance.WheelColors).ToHaveValueAsync(DefaultWheelColors);
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_APlayedPanelOpacityOfFifty_When_TheOverlayColorsResetIsConfirmed_Then_TheOpacityReturnsToSeventy()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var appearance = settings.Appearance;
        await settings.OpenAsync();
        await appearance.OpenAsync();
        await Expect(appearance.PlayedPanelOpacity).ToHaveValueAsync("70");
        await appearance.PlayedPanelOpacity.FillAsync("50");

        await appearance.RevertButton("Overlay Colors").ClickAsync();
        var review = settings.MessageBox("Review Overlay colors reset");
        await review.ExpectOpenAsync();
        // The opacity is stored as the alpha of the panel background colour.
        await Expect(review.ResetFields).ToHaveTextAsync(["Panel background color"]);
        await review.ChooseAsync("Reset draft to defaults");

        await Expect(appearance.PlayedPanelOpacity).ToHaveValueAsync("70");
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_SavedChangesInSeveralSections_When_ResetAllIsConfirmedAndSaved_Then_ReopenedSettingsShowDefaults()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.SelectPlayHistoryPeriodAsync("month");
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.BackgroundMode.SelectOptionAsync("transparent");
        await settings.SaveAsync();

        await settings.Advanced.OpenAsync();
        await settings.Advanced.ReviewResetAllButton.ClickAsync();
        var review = settings.MessageBox("Review All settings reset");
        await review.ExpectOpenAsync();
        await Expect(review.ResetSections).ToHaveTextAsync(["Spinner & Queue", "Played Songs panel", "Background"]);
        await Expect(review.ResetFields).ToHaveTextAsync(["Play history period", "Sequence numbers", "Mode"]);
        await review.ChooseAsync("Reset draft to defaults");
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
        await settings.SaveAsync();

        await scenario.Dashboard.OpenAsync();
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await Expect(settings.Spinner.PlayHistoryPeriod).ToHaveValueAsync("week");
        await settings.OverlayLayout.OpenAsync();
        await Expect(settings.OverlayLayout.PlayedShowNumbers).Not.ToBeCheckedAsync();
        await settings.Appearance.OpenAsync();
        await Expect(settings.Appearance.BackgroundMode).ToHaveValueAsync("color");
    }
}
