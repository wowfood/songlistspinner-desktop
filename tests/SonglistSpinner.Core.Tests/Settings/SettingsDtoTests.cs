using System.Text.Json;
using System.Text.Json.Nodes;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.Songs;
using Xunit;

namespace SonglistSpinner.Core.Tests.Settings;

public class SettingsDtoTests
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    [Fact]
    public void Given_LegacySettingsWithoutNumbering_When_Deserialized_Then_UsesSafeDefaults()
    {
        var settings = JsonSerializer.Deserialize<SettingsDto>("{}");

        Assert.NotNull(settings);
        Assert.False(settings.PlayedListShowNumbers);
        Assert.Equal(
            SpinnerSettingValues.PlayedListNumberingStarts.Bottom,
            settings.PlayedListNumberingStart);
        Assert.Equal(SongTextFormatting.DefaultSeparator, settings.PlayedListSeparator);
        Assert.Equal(SongTextFormatting.DefaultSeparator, settings.NowPlayingSeparator);
        Assert.True(settings.PlayedListShowLabels);
        Assert.True(settings.NowPlayingShowLabels);
        Assert.False(settings.PlayedListShowFieldHeaders);
    }

    [Fact]
    public void Given_DefaultSettings_When_Serialized_Then_StringBackedContractsRemainUnchanged()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new SettingsDto()));
        var root = document.RootElement;

        Assert.Equal("color", root.GetProperty(nameof(SettingsDto.BackgroundMode)).GetString());
        Assert.Equal("twitch", root.GetProperty(nameof(SettingsDto.StreamerPlatform)).GetString());
        Assert.Equal("week", root.GetProperty(nameof(SettingsDto.PlayHistoryPeriod)).GetString());
        Assert.Equal("right", root.GetProperty(nameof(SettingsDto.PlayedListPosition)).GetString());
        Assert.Equal("bottom-left", root.GetProperty(nameof(SettingsDto.NowPlayingPosition)).GetString());
        Assert.Equal("bottom", root.GetProperty(nameof(SettingsDto.PlayedListNumberingStart)).GetString());
        Assert.Equal(SongFieldNames.DefaultJson, root.GetProperty("SongListFields").GetString());
        Assert.Equal(
            SongTextFormatting.DefaultSeparator,
            root.GetProperty(nameof(SettingsDto.PlayedListSeparator)).GetString());
        Assert.Equal(
            SongTextFormatting.DefaultSeparator,
            root.GetProperty(nameof(SettingsDto.NowPlayingSeparator)).GetString());
        Assert.True(root.GetProperty(nameof(SettingsDto.PlayedListShowLabels)).GetBoolean());
        Assert.True(root.GetProperty(nameof(SettingsDto.NowPlayingShowLabels)).GetBoolean());
        Assert.False(root.GetProperty(nameof(SettingsDto.PlayedListShowFieldHeaders)).GetBoolean());
    }

    [Fact]
    public void Given_NoSavedSettings_When_DefaultsSerialized_Then_MatchThePreviouslyPersistedDefaults()
    {
        // A user with no saved settings gets these values; they must not change by accident.
        const string expected = """
            {
              "WheelColors": "[\"#ff6b6b\",\"#4ecdc4\",\"#45b7d1\",\"#f9ca24\",\"#6c5ce7\",\"#a29bfe\",\"#fd79a8\",\"#fdcb6e\"]",
              "BackgroundMode": "color",
              "BackgroundColor": "#111111",
              "BackgroundImage": "",
              "DefaultStreamerName": "",
              "StreamerPlatform": "twitch",
              "HideChangeOptionWhenDefault": true,
              "SongListFields": "[\"artist\",\"title\"]",
              "PlayedListSeparator": " | ",
              "PlayedListShowLabels": true,
              "PlayedListShowFieldHeaders": false,
              "ExcludePlayedSongs": false,
              "PlayedListPosition": "right",
              "PlayHistoryPeriod": "week",
              "AutoPlay": false,
              "DisplayNowPlaying": false,
              "NowPlayingFields": "[\"artist\",\"title\"]",
              "NowPlayingSeparator": " | ",
              "NowPlayingShowLabels": true,
              "NowPlayingFontFamily": "sans-serif",
              "NowPlayingFontSize": "1.125rem",
              "NowPlayingWidth": "28rem",
              "NowPlayingPosition": "bottom-left",
              "NowPlayingBackgroundOpacity": null,
              "WinnerDialogFields": null,
              "WinnerDialogFontFamily": "sans-serif",
              "WinnerDialogFontSize": "1rem",
              "WinnerDialogWidth": "36rem",
              "WinnerDialogShowQueuePosition": true,
              "DebugMode": false,
              "ColorText": "#ffffff",
              "ColorStatusBackground": "rgba(0, 0, 0, 0.7)",
              "ColorPlayedListBackground": "rgba(0, 0, 0, 0.7)",
              "ColorPlayedItemBackground": "#222222",
              "ColorResizeHandleBackground": "#333333",
              "ColorResizeHandleHoverBackground": "#555555",
              "ColorToggleBackground": "#222222",
              "ColorButtonBackground": "#555555",
              "ColorButtonText": "#CCCCCC",
              "ColorPointer": "wheat",
              "PlayedListFontFamily": "sans-serif",
              "PlayedListFontSize": "0.875rem",
              "PlayedListMaxLines": 2,
              "PlayedListShowNumbers": false,
              "PlayedListNumberingStart": "bottom"
            }
            """;

        var json = JsonSerializer.Serialize(new SettingsDto());

        Assert.Equal(ToComparableJson(expected), ToComparableJson(json));
    }

    [Theory]
    [InlineData(SavedSettingsFixture.Develop4f36e18Defaults)]
    [InlineData(SavedSettingsFixture.Develop4f36e18Customised)]
    public void Given_SettingsSavedByDevelop4f36e18_When_LoadedAndSavedAgain_Then_WritesTheSameJson(string fixtureName)
    {
        var savedJson = SavedSettingsFixture.ReadJson(fixtureName);

        var resavedJson = SavedSettingsFixture.Save(SavedSettingsFixture.Load(fixtureName));

        Assert.Equal(ToComparableJson(savedJson), ToComparableJson(resavedJson));
    }

    [Fact]
    public void Given_CustomisedSettingsSavedByDevelop4f36e18_When_Loaded_Then_KeepsEverySavedValue()
    {
        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Develop4f36e18Customised);

        Assert.Equal("""["#123456","#abcdef","#fedcba"]""", settings.WheelColors);
        Assert.Equal("transparent", settings.BackgroundMode);
        Assert.Equal("#202020", settings.BackgroundColor);
        Assert.Equal("https://example.com/stage.png", settings.BackgroundImage);
        Assert.Equal("examplestreamer", settings.DefaultStreamerName);
        Assert.Equal("youtube", settings.StreamerPlatform);
        Assert.False(settings.HideChangeOptionWhenDefault);
        Assert.Equal("""["title","requester","donation"]""", settings.PlayedListFields);
        Assert.Equal(" • ", settings.PlayedListSeparator);
        Assert.False(settings.PlayedListShowLabels);
        Assert.True(settings.PlayedListShowFieldHeaders);
        Assert.True(settings.ExcludePlayedSongs);
        Assert.Equal("left", settings.PlayedListPosition);
        Assert.Equal("month", settings.PlayHistoryPeriod);
        Assert.True(settings.UpdateQueueAfterSpin);
        Assert.True(settings.DisplayNowPlaying);
        Assert.Equal("""["requester","artist"]""", settings.NowPlayingFields);
        Assert.Equal(" / ", settings.NowPlayingSeparator);
        Assert.False(settings.NowPlayingShowLabels);
        Assert.Equal("Georgia, serif", settings.NowPlayingFontFamily);
        Assert.Equal("1.5rem", settings.NowPlayingFontSize);
        Assert.Equal("40rem", settings.NowPlayingWidth);
        Assert.Equal("top-right", settings.NowPlayingPosition);
        Assert.Equal(0.35, settings.NowPlayingBackgroundOpacity);
        Assert.Equal("""["donation","title"]""", settings.WinnerDialogFields);
        Assert.Equal("Verdana", settings.WinnerDialogFontFamily);
        Assert.Equal("1.25rem", settings.WinnerDialogFontSize);
        Assert.Equal("42rem", settings.WinnerDialogWidth);
        Assert.False(settings.WinnerDialogShowQueuePosition);
        Assert.True(settings.DebugMode);
        Assert.Equal("#eeeeee", settings.ColorText);
        Assert.Equal("rgba(10, 20, 30, 0.5)", settings.ColorStatusBackground);
        Assert.Equal("#101820", settings.ColorPlayedListBackground);
        Assert.Equal("#303030", settings.ColorPlayedItemBackground);
        Assert.Equal("#404040", settings.ColorResizeHandleBackground);
        Assert.Equal("#606060", settings.ColorResizeHandleHoverBackground);
        Assert.Equal("#505050", settings.ColorToggleBackground);
        Assert.Equal("#707070", settings.ColorButtonBackground);
        Assert.Equal("#000000", settings.ColorButtonText);
        Assert.Equal("#ff0000", settings.ColorPointer);
        Assert.Equal("Arial", settings.PlayedListFontFamily);
        Assert.Equal("1rem", settings.PlayedListFontSize);
        Assert.Equal(4, settings.PlayedListMaxLines);
        Assert.True(settings.PlayedListShowNumbers);
        Assert.Equal("top", settings.PlayedListNumberingStart);
    }

    [Fact]
    public void Given_CustomisedSettingsSavedByRelease120_When_Loaded_Then_KeepsEverySavedValue()
    {
        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Release120Customised);

        Assert.Equal("""["#123456","#abcdef"]""", settings.WheelColors);
        Assert.Equal("transparent", settings.BackgroundMode);
        Assert.Equal("#202020", settings.BackgroundColor);
        Assert.Equal("https://example.com/stage.png", settings.BackgroundImage);
        Assert.Equal("examplestreamer", settings.DefaultStreamerName);
        Assert.Equal("youtube", settings.StreamerPlatform);
        Assert.False(settings.HideChangeOptionWhenDefault);
        Assert.Equal("""["title","donation"]""", settings.PlayedListFields);
        Assert.True(settings.ExcludePlayedSongs);
        Assert.Equal("left", settings.PlayedListPosition);
        Assert.Equal("month", settings.PlayHistoryPeriod);
        Assert.True(settings.UpdateQueueAfterSpin);
        Assert.True(settings.DisplayNowPlaying);
        Assert.Equal("""["requester","artist"]""", settings.NowPlayingFields);
        Assert.Equal("Georgia, serif", settings.NowPlayingFontFamily);
        Assert.Equal("1.5rem", settings.NowPlayingFontSize);
        Assert.Equal("40rem", settings.NowPlayingWidth);
        Assert.Equal("top-right", settings.NowPlayingPosition);
        Assert.True(settings.DebugMode);
        Assert.Equal("#eeeeee", settings.ColorText);
        Assert.Equal("rgba(10, 20, 30, 0.5)", settings.ColorStatusBackground);
        Assert.Equal("#101820", settings.ColorPlayedListBackground);
        Assert.Equal("#303030", settings.ColorPlayedItemBackground);
        Assert.Equal("#404040", settings.ColorResizeHandleBackground);
        Assert.Equal("#606060", settings.ColorResizeHandleHoverBackground);
        Assert.Equal("#505050", settings.ColorToggleBackground);
        Assert.Equal("#707070", settings.ColorButtonBackground);
        Assert.Equal("#000000", settings.ColorButtonText);
        Assert.Equal("#ff0000", settings.ColorPointer);
        Assert.Equal("Arial", settings.PlayedListFontFamily);
        Assert.Equal("1rem", settings.PlayedListFontSize);
        Assert.Equal(4, settings.PlayedListMaxLines);
    }

    [Fact]
    public void Given_SettingsSavedByRelease120_When_Loaded_Then_PropertiesAddedSinceTakeTheirDefaults()
    {
        var defaults = new SettingsDto();

        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Release120Customised);

        Assert.Equal(defaults.PlayedListSeparator, settings.PlayedListSeparator);
        Assert.Equal(defaults.PlayedListShowLabels, settings.PlayedListShowLabels);
        Assert.Equal(defaults.PlayedListShowFieldHeaders, settings.PlayedListShowFieldHeaders);
        Assert.Equal(defaults.PlayedListShowNumbers, settings.PlayedListShowNumbers);
        Assert.Equal(defaults.PlayedListNumberingStart, settings.PlayedListNumberingStart);
        Assert.Equal(defaults.NowPlayingSeparator, settings.NowPlayingSeparator);
        Assert.Equal(defaults.NowPlayingShowLabels, settings.NowPlayingShowLabels);
        Assert.Null(settings.NowPlayingBackgroundOpacity);
        // Null marks settings saved before the winner dialog had its own fields; see SettingsDtoNormalizer.
        Assert.Null(settings.WinnerDialogFields);
        Assert.Equal(defaults.WinnerDialogFontFamily, settings.WinnerDialogFontFamily);
        Assert.Equal(defaults.WinnerDialogFontSize, settings.WinnerDialogFontSize);
        Assert.Equal(defaults.WinnerDialogWidth, settings.WinnerDialogWidth);
        Assert.Equal(defaults.WinnerDialogShowQueuePosition, settings.WinnerDialogShowQueuePosition);
    }

    // Saving adds the properties 1.2.0 did not have, so the saved JSON is a superset rather than the same bytes.
    [Fact]
    public void Given_SettingsSavedByRelease120_When_LoadedAndSavedAgain_Then_KeepsEverySavedKeyAndValue()
    {
        var savedJson = JsonNode.Parse(SavedSettingsFixture.ReadJson(SavedSettingsFixture.Release120Customised))!
            .AsObject();

        var resavedJson = JsonNode.Parse(
                SavedSettingsFixture.Save(SavedSettingsFixture.Load(SavedSettingsFixture.Release120Customised)))!
            .AsObject();

        var missingOrChanged = savedJson
            .Where(saved => !resavedJson.TryGetPropertyValue(saved.Key, out var resaved) ||
                            !JsonNode.DeepEquals(saved.Value, resaved))
            .Select(saved => saved.Key);
        Assert.Empty(missingOrChanged);
    }

    private static string ToComparableJson(string json) =>
        JsonNode.Parse(json)!.ToJsonString(IndentedJsonOptions);
}
