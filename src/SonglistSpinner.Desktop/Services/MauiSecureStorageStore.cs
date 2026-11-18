namespace SonglistSpinner.Services;

/// <summary>The app's secrets in MAUI secure storage (Windows data protection).</summary>
public sealed class MauiSecureStorageStore(ISecureStorage secureStorage) : ISecretStore
{
    public Task<string?> GetAsync(string key) => secureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) => secureStorage.SetAsync(key, value);

    public void Remove(string key) => secureStorage.Remove(key);
}
