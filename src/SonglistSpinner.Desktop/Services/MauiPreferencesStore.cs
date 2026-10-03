namespace SonglistSpinner.Services;

/// <summary>The app's saved values in MAUI preferences.</summary>
public sealed class MauiPreferencesStore(IPreferences preferences) : IKeyValueStore
{
    public string? GetValue(string key) => preferences.Get<string?>(key, null);

    public void SetValue(string key, string value) => preferences.Set(key, value);

    public void Remove(string key) => preferences.Remove(key);
}
