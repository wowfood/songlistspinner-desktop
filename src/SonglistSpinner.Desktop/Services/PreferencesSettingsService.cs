using System.Text.Json;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Services;

public sealed class PreferencesSettingsService : ILocalSettingsService
{
    private const string SettingsKey = "local_settings";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    public SettingsDto LoadSettings()
    {
        var json = Preferences.Get(SettingsKey, null);
        if (string.IsNullOrEmpty(json)) return new SettingsDto();
        try
        {
            return SettingsDtoNormalizer.Normalize(
                JsonSerializer.Deserialize<SettingsDto>(json, JsonOpts) ?? new SettingsDto());
        }
        catch
        {
            return new SettingsDto();
        }
    }

    public void SaveSettings(SettingsDto dto)
    {
        Preferences.Set(SettingsKey, JsonSerializer.Serialize(SettingsDtoNormalizer.Normalize(dto), JsonOpts));
    }

    public SpinnerConfig ToSpinnerConfig(SettingsDto dto) => SettingsDtoConverter.ToSpinnerConfig(dto);
}
