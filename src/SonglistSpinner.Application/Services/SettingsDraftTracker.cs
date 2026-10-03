using System.Text.Json;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Services;

/// <summary>
/// Tells whether the Settings page's draft differs from what was last loaded or saved. The settings and the
/// API credential are tracked apart because they are saved apart: clearing the credential saves only that.
/// Nothing counts as changed until the first <c>Mark…Saved</c> call.
/// </summary>
/// <remarks>
/// The settings draft is the <see cref="SettingsDto"/> plus the view model's editor state (wheel colours text,
/// field order and selection, panel background and opacity), which Save writes into the DTO. The DTO is compared
/// as Save would write it.
/// </remarks>
public sealed class SettingsDraftTracker
{
    private string? _savedSettings;
    private CredentialDraft? _savedCredential;

    public void MarkSettingsSaved(SettingsDto settings, SettingsViewModel viewModel) =>
        _savedSettings = Capture(settings, viewModel);

    public void MarkCredentialSaved(CredentialDraft credential) => _savedCredential = credential;

    public bool HasUnsavedSettingsChanges(SettingsDto settings, SettingsViewModel viewModel) =>
        _savedSettings is not null &&
        !StringComparer.Ordinal.Equals(_savedSettings, Capture(settings, viewModel));

    public bool HasUnsavedCredentialChanges(CredentialDraft credential) =>
        _savedCredential is not null && _savedCredential != credential;

    private static string Capture(SettingsDto settings, SettingsViewModel viewModel) =>
        JsonSerializer.Serialize(new SettingsSnapshot(
            CaptureAsSaved(settings, viewModel),
            viewModel.WheelColorsRaw,
            CaptureFields(viewModel.DisplayFields),
            CaptureFields(viewModel.NowPlayingDisplayFields),
            CaptureFields(viewModel.WinnerDialogDisplayFields),
            viewModel.PlayedListBgHex,
            viewModel.PlayedListBgAlpha,
            viewModel.UseIndependentNowPlayingBgAlpha,
            viewModel.NowPlayingBgAlpha));

    // The DTO as Save would write it. The page writes the editor state into the DTO at other times too (before
    // reviewing a reset, and in the defaults a reset applies), which re-encodes fields such as the panel colour and
    // the winner dialog fields without changing them; capturing the raw DTO would count that as a change.
    private static string CaptureAsSaved(SettingsDto settings, SettingsViewModel viewModel)
    {
        var copy = JsonSerializer.Deserialize<SettingsDto>(JsonSerializer.Serialize(settings)) ?? new SettingsDto();
        viewModel.ApplyToDto(copy);
        return JsonSerializer.Serialize(copy);
    }

    private static FieldSnapshot[] CaptureFields(IEnumerable<DisplayField> fields) =>
        fields.Select(field => new FieldSnapshot(field.Name, field.Selected)).ToArray();

    private sealed record FieldSnapshot(string Name, bool Selected);

    private sealed record SettingsSnapshot(
        string Settings,
        string WheelColors,
        FieldSnapshot[] PlayedListFields,
        FieldSnapshot[] NowPlayingFields,
        FieldSnapshot[] WinnerDialogFields,
        string PlayedListBackground,
        double PlayedListBackgroundAlpha,
        bool UseIndependentNowPlayingBackgroundAlpha,
        double NowPlayingBackgroundAlpha);
}
