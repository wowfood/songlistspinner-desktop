namespace SonglistSpinner.Services;

/// <summary>
/// TEST ONLY: secrets kept unencrypted in a JSON file of the isolated test profile
/// (<see cref="EnvironmentOverrides.ProfileDirectory"/>), so an app under test never reads or replaces the
/// credential in the user's Windows secure storage. Never use it for a real credential.
/// </summary>
public sealed class PlaintextFileSecretStore(string path) : ISecretStore
{
    private readonly JsonFileKeyValueStore _file = new(path);

    public Task<string?> GetAsync(string key) => Task.FromResult(_file.GetValue(key));

    public Task SetAsync(string key, string value)
    {
        _file.SetValue(key, value);
        return Task.CompletedTask;
    }

    public void Remove(string key) => _file.Remove(key);
}
