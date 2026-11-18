using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class SettingsDraftTrackerTests
{
    private static readonly CredentialDraft SavedCredential =
        new(StreamerSongListCredentialKind.Streamer, "", TokenEntered: false);

    [Fact]
    public void Given_NothingMarkedSaved_When_TheDraftChanges_Then_ReportsNoUnsavedChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();

        settings.DebugMode = true;

        Assert.False(tracker.HasUnsavedSettingsChanges(settings, viewModel));
        Assert.False(tracker.HasUnsavedCredentialChanges(SavedCredential with { TokenEntered = true }));
    }

    [Fact]
    public void Given_SavedSettings_When_ASettingChanges_Then_ReportsUnsavedSettingsChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);

        settings.DebugMode = true;

        Assert.True(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_SavedSettings_When_OnlyTheFieldEditorChanges_Then_ReportsUnsavedSettingsChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);

        viewModel.ToggleField(viewModel.DisplayFields[0].Name);

        Assert.True(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_SavedSettings_When_AChangeIsUndone_Then_ReportsNoUnsavedSettingsChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);
        var savedOpacity = viewModel.PlayedListBgAlpha;

        viewModel.PlayedListBgAlpha = savedOpacity / 2;
        viewModel.PlayedListBgAlpha = savedOpacity;

        Assert.False(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_ChangedSettings_When_MarkedSaved_Then_ReportsNoUnsavedSettingsChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);
        settings.DebugMode = true;

        tracker.MarkSettingsSaved(settings, viewModel);

        Assert.False(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_SavedSettings_When_TheEditorStateIsWrittenToTheDraft_Then_ReportsNoUnsavedSettingsChanges()
    {
        // Settings writes the editor state into the draft before it reviews a reset, even when nothing differs.
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);

        viewModel.ApplyToDto(settings);

        Assert.False(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_SavedDefaults_When_TheDraftIsResetToDefaults_Then_ReportsNoUnsavedSettingsChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);
        settings.PlayedListShowNumbers = true;

        // What Settings does when a reset is confirmed: the defaults, as the editor writes them, replace the draft.
        var defaults = new SettingsDto();
        var defaultsEditor = new SettingsViewModel();
        defaultsEditor.Initialize(defaults);
        defaultsEditor.ApplyToDto(defaults);
        SettingsResetPlan.ApplyDefaults(settings, defaults);
        viewModel.Initialize(settings);

        Assert.False(tracker.HasUnsavedSettingsChanges(settings, viewModel));
    }

    [Fact]
    public void Given_SavedCredential_When_ATokenIsEntered_Then_ReportsUnsavedCredentialChanges()
    {
        var tracker = new SettingsDraftTracker();
        tracker.MarkCredentialSaved(SavedCredential);

        var entered = tracker.HasUnsavedCredentialChanges(SavedCredential with { TokenEntered = true });

        Assert.True(entered);
    }

    [Fact]
    public void Given_SavedCredential_When_TheSameFieldsAreChecked_Then_ReportsNoUnsavedCredentialChanges()
    {
        var tracker = new SettingsDraftTracker();
        tracker.MarkCredentialSaved(SavedCredential);

        var changed = tracker.HasUnsavedCredentialChanges(
            new CredentialDraft(StreamerSongListCredentialKind.Streamer, "", TokenEntered: false));

        Assert.False(changed);
    }

    [Fact]
    public void Given_SavedCredential_When_OnlyTheSettingsChange_Then_ReportsNoUnsavedCredentialChanges()
    {
        var tracker = new SettingsDraftTracker();
        var (settings, viewModel) = LoadedDraft();
        tracker.MarkSettingsSaved(settings, viewModel);
        tracker.MarkCredentialSaved(SavedCredential);

        settings.DebugMode = true;

        Assert.False(tracker.HasUnsavedCredentialChanges(SavedCredential));
    }

    private static (SettingsDto Settings, SettingsViewModel ViewModel) LoadedDraft()
    {
        var settings = new SettingsDto();
        var viewModel = new SettingsViewModel();
        viewModel.Initialize(settings);
        return (settings, viewModel);
    }
}
