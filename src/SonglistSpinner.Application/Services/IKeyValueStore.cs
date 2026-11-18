namespace SonglistSpinner.Services;

/// <summary>
/// The machine's saved, non-secret app values (MAUI preferences in the app). The keys are persisted contracts.
/// </summary>
public interface IKeyValueStore
{
    /// <returns>The saved value, or null when the key has none.</returns>
    string? GetValue(string key);

    void SetValue(string key, string value);

    void Remove(string key);
}
