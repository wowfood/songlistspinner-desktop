using System.Text.Json;

namespace SonglistSpinner.Services;

/// <summary>
/// Saved values in one JSON file, for the isolated test profile (<see cref="EnvironmentOverrides.ProfileDirectory"/>)
/// that stands in for MAUI preferences. Every change rewrites the whole file, so a new instance over the same
/// path (the next launch) reads back what the last one saved.
/// </summary>
public sealed class JsonFileKeyValueStore : IKeyValueStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly Dictionary<string, string> _values;

    public JsonFileKeyValueStore(string path)
    {
        _path = path;
        _values = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []
            : [];
    }

    public string? GetValue(string key)
    {
        lock (_gate)
            return _values.GetValueOrDefault(key);
    }

    public void SetValue(string key, string value)
    {
        lock (_gate)
        {
            _values[key] = value;
            Save();
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (_values.Remove(key)) Save();
        }
    }

    // Callers hold _gate. Writing beside the file and moving it over keeps a crash from leaving half a file.
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_values));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
