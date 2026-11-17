using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class PreferencesSettingsServiceTests
{
    private const string SettingsKey = "local_settings";
    private const string UnreadableSettingsKey = "local_settings.unreadable";

    [Fact]
    public void Given_NothingSaved_When_Loading_Then_TheDefaultsAreReturned()
    {
        var settings = new PreferencesSettingsService(new InMemoryKeyValueStore());

        var loaded = settings.LoadSettings();

        Assert.Equal(Json(new SettingsDto()), Json(loaded));
    }

    [Fact]
    public void Given_UnreadableSavedSettings_When_Loading_Then_TheDefaultsAreReturned()
    {
        var store = new InMemoryKeyValueStore { Values = { [SettingsKey] = "{ not json" } };
        var settings = new PreferencesSettingsService(store);

        var loaded = settings.LoadSettings();

        Assert.Equal(Json(new SettingsDto()), Json(loaded));
    }

    [Fact]
    public void Given_UnreadableSavedSettings_When_LoadedAndThenSaved_Then_TheUnreadableValueIsKeptAside()
    {
        // One field of the wrong JSON type makes the whole value unreadable, though the rest is recoverable.
        const string unreadable = """{"PlayedListMaxLines":"many","StreamerName":"wowfood"}""";
        var store = new InMemoryKeyValueStore { Values = { [SettingsKey] = unreadable } };
        var settings = new PreferencesSettingsService(store);

        settings.SaveSettings(settings.LoadSettings());

        Assert.Equal(unreadable, store.Values[UnreadableSettingsKey]);
        Assert.NotEqual(unreadable, store.Values[SettingsKey]);
    }

    [Fact]
    public void Given_UnreadableSavedSettings_When_Loading_Then_TheFailureIsLoggedWithoutTheSavedValue()
    {
        var store = new InMemoryKeyValueStore { Values = { [SettingsKey] = """{"PlayedListMaxLines":"many"}""" } };
        var logger = new FakeLogger<PreferencesSettingsService>();
        var settings = new PreferencesSettingsService(store, logger);

        settings.LoadSettings();

        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(
            "The saved settings could not be read, so the defaults are used. The unreadable value is kept under local_settings.unreadable",
            entry.Message);
        Assert.IsType<JsonException>(entry.Exception);
    }

    [Fact]
    public void Given_UnreadableSavedSettingsThatCannotBeKeptAside_When_Loading_Then_TheDefaultsAreStillReturned()
    {
        var settings = new PreferencesSettingsService(new ReadOnlyStore(savedSettings: "{ not json"));

        var loaded = settings.LoadSettings();

        Assert.Equal(Json(new SettingsDto()), Json(loaded));
    }

    [Fact]
    public void Given_EditedSettings_When_Saved_Then_TheyAreStoredNormalizedUnderTheirWireNamesAndLoadBack()
    {
        var store = new InMemoryKeyValueStore();
        var settings = new PreferencesSettingsService(store);
        var edited = new SettingsDto
        {
            PlayHistoryPeriod = "MONTH",
            PlayedListFields = """["Requester","title"]""",
            UpdateQueueAfterSpin = true
        };

        settings.SaveSettings(edited);
        var loaded = settings.LoadSettings();

        using var saved = JsonDocument.Parse(Assert.Single(store.Values, entry => entry.Key == SettingsKey).Value);
        Assert.Equal("month", saved.RootElement.GetProperty("PlayHistoryPeriod").GetString());
        Assert.Equal("""["requester","title"]""", saved.RootElement.GetProperty("SongListFields").GetString());
        Assert.True(saved.RootElement.GetProperty("AutoPlay").GetBoolean());
        Assert.Equal(Json(edited), Json(loaded));
    }

    private static string Json(SettingsDto settings) => JsonSerializer.Serialize(settings);

    /// <summary>Saved values that can be read but not written, as when the preferences file is locked.</summary>
    private sealed class ReadOnlyStore(string savedSettings) : IKeyValueStore
    {
        public string? GetValue(string key) => key == SettingsKey ? savedSettings : null;

        public void SetValue(string key, string value) => throw new IOException("The preferences file is read-only.");

        public void Remove(string key) => throw new IOException("The preferences file is read-only.");
    }
}
