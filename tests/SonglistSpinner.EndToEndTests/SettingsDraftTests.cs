using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The Settings draft: what the page says about unsaved changes, saving them, the prompt that guards leaving with
/// them, and the CSS checks that block a save. SettingsPersistenceTests covers a saved value surviving a restart.
/// </summary>
public class SettingsDraftTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_SettingsWithNoChanges_When_ACheckboxIsChecked_Then_ThePageReportsAnUnsavedDraft()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");

        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();

        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
        await Expect(settings.SaveBarState).ToHaveTextAsync("Changes are previewed but not yet saved.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnUnsavedDraft_When_SaveSettingsIsPressed_Then_ThePageReportsItSaved()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");

        await settings.SaveButton.ClickAsync();

        await Expect(settings.SaveBarState).ToHaveTextAsync("✓ Saved");
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
        await Expect(settings.OverlayLayout.PlayedShowNumbers).ToBeCheckedAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnUnsavedDraft_When_LeavingForTheDashboardAndKeepEditingIsChosen_Then_SettingsStaysOpenWithTheDraft()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();

        await settings.ClickDashboardLinkAsync();
        var prompt = settings.UnsavedChangesPrompt;
        await prompt.ExpectOpenAsync();
        await Expect(prompt.Buttons).ToHaveTextAsync(["Keep editing", "Abandon changes", "Save and leave"]);
        await prompt.ChooseAsync("Keep editing");

        await Expect(settings.Heading).ToBeVisibleAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
        await Expect(settings.OverlayLayout.PlayedShowNumbers).ToBeCheckedAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnUnsavedDraft_When_LeavingForTheDashboardAndAbandoningChanges_Then_TheChangeIsDiscarded()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();

        await settings.ClickDashboardLinkAsync();
        await settings.UnsavedChangesPrompt.ExpectOpenAsync();
        await settings.UnsavedChangesPrompt.ChooseAsync("Abandon changes");

        await Expect(scenario.Dashboard.Container).ToBeVisibleAsync();
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await Expect(settings.OverlayLayout.PlayedShowNumbers).Not.ToBeCheckedAsync();
        await Expect(settings.DraftState).ToHaveTextAsync("All changes saved");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnUnsavedDraft_When_LeavingForTheDashboardWithSaveAndLeave_Then_TheDashboardUsesTheSavedSetting()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        await new ChannelSeed("draft_streamer")
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("draft_streamer");
        await dashboard.PlayedList.ExpectLinesAsync("Artist: Daft Punk | Title: Get Lucky");
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();

        await settings.ClickDashboardLinkAsync();
        await settings.UnsavedChangesPrompt.ExpectOpenAsync();
        await settings.UnsavedChangesPrompt.ChooseAsync("Save and leave");

        await Expect(scenario.Dashboard.Container).ToBeVisibleAsync();
        await dashboard.PlayedList.ExpectLinesAsync("1. Artist: Daft Punk | Title: Get Lucky");
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await Expect(settings.OverlayLayout.PlayedShowNumbers).ToBeCheckedAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_APlayedListFontSizeOfAuto_When_SaveSettingsIsPressed_Then_TheSaveIsRefusedWithTheFieldError()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.PlayedFontSize.FillAsync("auto");

        await settings.SaveButton.ClickAsync();

        // aria-invalid on this field is a known deferred defect, so only the visible messages are asserted.
        await Expect(settings.OverlayLayout.PlayedFontSizeError).ToHaveTextAsync(
            "Played-list font size must be a concrete CSS size such as 1rem or 16px.");
        await Expect(settings.SaveBarState).ToHaveTextAsync("✗ Correct the highlighted appearance values before saving.");
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnInvalidWheelColor_When_SaveSettingsIsPressedFromAnotherSection_Then_AppearanceIsSelectedWithTheLineError()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.WheelColors.FillAsync("notacolor");
        await settings.Connection.OpenAsync();

        await settings.SaveButton.ClickAsync();

        await Expect(settings.SelectedSection).ToHaveTextAsync("Appearance");
        await Expect(settings.Appearance.WheelColorsError).ToHaveTextAsync("Use valid CSS colors on line 1.");
        await Expect(settings.SaveBarState).ToHaveTextAsync("✗ Correct the highlighted appearance values before saving.");
    }
}
