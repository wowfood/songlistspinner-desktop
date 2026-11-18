namespace SonglistSpinner.Services;

/// <summary>
/// The machine's saved secrets (Windows secure storage in the app). The keys are persisted contracts.
/// </summary>
public interface ISecretStore
{
    /// <returns>The saved secret, or null when the key has none.</returns>
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}
