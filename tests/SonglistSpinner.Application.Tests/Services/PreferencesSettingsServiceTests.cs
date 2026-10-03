using System.Text.Json;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class PreferencesSettingsServiceTests
{
    private const string SettingsKey = "local_settings";

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
}
