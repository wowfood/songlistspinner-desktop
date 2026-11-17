using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Services;

/// <summary>
/// Loads and saves the user's settings as normalized <see cref="SettingsDto"/> JSON in MAUI preferences.
/// The JSON is a persisted contract: renaming a property needs its wire name kept or a migration.
/// </summary>
public sealed class PreferencesSettingsService(
    IKeyValueStore preferences,
    ILogger<PreferencesSettingsService>? logger = null)
{
    private const string SettingsKey = "local_settings";
    private const string UnreadableSettingsKey = "local_settings.unreadable";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<PreferencesSettingsService> _logger = logger ?? NullLogger<PreferencesSettingsService>.Instance;

    /// <summary>
    /// Returns the saved settings, or the defaults when nothing is saved or the saved value cannot be read.
    /// An unreadable value is first copied to <c>local_settings.unreadable</c>, because the next save replaces
    /// it with the defaults the user then sees. A read failure never escapes: the app loads its settings while
    /// it starts.
    /// </summary>
    public SettingsDto LoadSettings()
    {
        var json = preferences.GetValue(SettingsKey);
        if (string.IsNullOrEmpty(json)) return new SettingsDto();
        try
        {
            return SettingsDtoNormalizer.NormalizeInPlace(
                JsonSerializer.Deserialize<SettingsDto>(json, JsonOpts) ?? new SettingsDto());
        }
        catch (Exception ex)
        {
            KeepUnreadableSettings(json, ex);
            return new SettingsDto();
        }
    }

    private void KeepUnreadableSettings(string json, Exception readFailure)
    {
        try
        {
            preferences.SetValue(UnreadableSettingsKey, json);
        }
        catch (Exception keepFailure)
        {
            _logger.LogWarning(
                keepFailure,
                "The saved settings could not be read or kept aside, so the defaults are used and the next save replaces them");
            return;
        }

        _logger.LogWarning(
            readFailure,
            "The saved settings could not be read, so the defaults are used. The unreadable value is kept under {UnreadableSettingsKey}",
            UnreadableSettingsKey);
    }

    public void SaveSettings(SettingsDto dto)
    {
        preferences.SetValue(SettingsKey, JsonSerializer.Serialize(SettingsDtoNormalizer.NormalizeInPlace(dto), JsonOpts));
    }
}
