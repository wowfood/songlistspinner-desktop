using SonglistSpinner.Services;

namespace SonglistSpinner.Application.Tests;

/// <summary>Saved secrets held in memory, so a test can read exactly which keys were written.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => Values.Remove(key);
}
