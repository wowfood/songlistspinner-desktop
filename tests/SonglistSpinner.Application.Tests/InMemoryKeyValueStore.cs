using SonglistSpinner.Services;

namespace SonglistSpinner.Application.Tests;

/// <summary>Saved app values held in memory, so a test can read exactly which keys were written.</summary>
internal sealed class InMemoryKeyValueStore : IKeyValueStore
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string key) => Values.GetValueOrDefault(key);

    public void SetValue(string key, string value) => Values[key] = value;

    public void Remove(string key) => Values.Remove(key);
}
