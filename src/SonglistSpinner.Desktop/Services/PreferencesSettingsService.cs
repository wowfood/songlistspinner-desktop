using System.Text.Json;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Services;

/// <summary>
/// Loads and saves the user's settings as normalized <see cref="SettingsDto"/> JSON in MAUI preferences.
/// The JSON is a persisted contract: renaming a property needs its wire name kept or a migration.
/// </summary>
public sealed class PreferencesSettingsService(IPreferences preferences)
{
    private const string SettingsKey = "local_settings";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SettingsDto LoadSettings()
    {
        var json = preferences.Get<string?>(SettingsKey, null);
        if (string.IsNullOrEmpty(json)) return new SettingsDto();
        try
        {
            return SettingsDtoNormalizer.NormalizeInPlace(
                JsonSerializer.Deserialize<SettingsDto>(json, JsonOpts) ?? new SettingsDto());
        }
        catch
        {
            return new SettingsDto();
        }
    }

    public void SaveSettings(SettingsDto dto)
    {
        preferences.Set(SettingsKey, JsonSerializer.Serialize(SettingsDtoNormalizer.NormalizeInPlace(dto), JsonOpts));
    }
}
