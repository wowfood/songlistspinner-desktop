using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Services;

public interface ILocalSettingsService
{
    SpinnerConfig CurrentConfig { get; }
    SettingsDto LoadSettings();
    void SaveSettings(SettingsDto dto);
    string GetApiBaseUrl();
    void SetApiBaseUrl(string url);
    SpinnerConfig ToSpinnerConfig(SettingsDto dto);
}