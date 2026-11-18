using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Desktop.Tests.Services;

/// <remarks>
/// Every released version kept its values in the app's own preferences, under the key alone. Reading or writing
/// anywhere else would lose each user's saved settings on upgrade.
/// </remarks>
public class MauiPreferencesStoreTests
{
    private const string Key = "local_settings";

    [Fact]
    public void Given_AValueInTheAppsOwnPreferences_When_Read_Then_ItIsReturned()
    {
        var preferences = new RecordingPreferences();
        preferences.Set(Key, "saved settings");
        var store = new MauiPreferencesStore(preferences);

        var value = store.GetValue(Key);

        Assert.Equal("saved settings", value);
    }

    [Fact]
    public void Given_TheKeyOnlyInASharedContainer_When_Read_Then_ThereIsNoValue()
    {
        var preferences = new RecordingPreferences();
        preferences.Set(Key, "someone else's", sharedName: "shared");
        var store = new MauiPreferencesStore(preferences);

        var value = store.GetValue(Key);

        Assert.Null(value);
    }

    [Fact]
    public void Given_AValue_When_Set_Then_ItIsWrittenToTheAppsOwnPreferencesUnderItsKey()
    {
        var preferences = new RecordingPreferences();
        var store = new MauiPreferencesStore(preferences);

        store.SetValue(Key, "new settings");

        Assert.Equal([(null, Key, "new settings")], preferences.Entries);
    }

    [Fact]
    public void Given_ASavedValue_When_Removed_Then_ItIsGoneFromTheAppsOwnPreferences()
    {
        var preferences = new RecordingPreferences();
        preferences.Set(Key, "saved settings");
        preferences.Set(Key, "someone else's", sharedName: "shared");
        var store = new MauiPreferencesStore(preferences);

        store.Remove(Key);

        Assert.Equal([("shared", Key, "someone else's")], preferences.Entries);
    }

    /// <summary>Preferences held in memory, recording which container each value is in.</summary>
    private sealed class RecordingPreferences : IPreferences
    {
        private readonly Dictionary<(string? SharedName, string Key), object?> _values = [];

        public IEnumerable<(string? SharedName, string Key, object? Value)> Entries =>
            _values.Select(entry => (entry.Key.SharedName, entry.Key.Key, entry.Value));

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey((sharedName, key));

        public void Remove(string key, string? sharedName = null) => _values.Remove((sharedName, key));

        public void Clear(string? sharedName = null)
        {
            foreach (var entry in _values.Keys.Where(entry => entry.SharedName == sharedName).ToList())
                _values.Remove(entry);
        }

        public void Set<T>(string key, T value, string? sharedName = null) => _values[(sharedName, key)] = value;

        public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
            _values.TryGetValue((sharedName, key), out var value) ? (T)value! : defaultValue;
    }
}
