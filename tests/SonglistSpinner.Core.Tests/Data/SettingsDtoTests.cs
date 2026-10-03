using System.Text.Json;
using System.Text.Json.Nodes;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Data;

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
        Assert.Equal(SongFieldNames.DefaultJson, root.GetProperty(nameof(SettingsDto.SongListFields)).GetString());
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

    [Fact]
    public void Given_InvalidPersistedValues_When_Normalized_Then_UsesCanonicalSafeDefaults()
    {
        var settings = new SettingsDto
        {
            BackgroundMode = "unknown",
            StreamerPlatform = "unknown",
            PlayedListPosition = "unknown",
            PlayHistoryPeriod = "unknown",
            NowPlayingPosition = "unknown",
            PlayedListNumberingStart = "unknown"
        };

        SettingsDtoNormalizer.Normalize(settings);

        Assert.Equal("color", settings.BackgroundMode);
        Assert.Equal("twitch", settings.StreamerPlatform);
        Assert.Equal("right", settings.PlayedListPosition);
        Assert.Equal("week", settings.PlayHistoryPeriod);
        Assert.Equal("bottom-left", settings.NowPlayingPosition);
        Assert.Equal("bottom", settings.PlayedListNumberingStart);
    }

    [Fact]
    public void Given_LegacyAndMixedCaseSettings_When_Normalized_Then_PreservesCanonicalFieldOrder()
    {
        var settings = new SettingsDto
        {
            BackgroundMode = "TRANSPARANT",
            StreamerPlatform = " YouTube ",
            SongListFields = """["DONATION","artist","donation","unknown"]""",
            NowPlayingFields = "[]",
            WinnerDialogFields = """["REQUESTER","unknown","Title","requester"]"""
        };

        SettingsDtoNormalizer.Normalize(settings);

        Assert.Equal("transparent", settings.BackgroundMode);
        Assert.Equal("youtube", settings.StreamerPlatform);
        Assert.Equal("""["donation","artist"]""", settings.SongListFields);
        Assert.Equal(SongFieldNames.DefaultJson, settings.NowPlayingFields);
        Assert.Equal("""["requester","title"]""", settings.WinnerDialogFields);
    }

    [Fact]
    public void Given_LegacySettingsWithoutWinnerFields_When_Normalized_Then_PreservesMigrationSignal()
    {
        var settings = new SettingsDto { WinnerDialogFields = null };

        SettingsDtoNormalizer.Normalize(settings);

        Assert.Null(settings.WinnerDialogFields);
    }

    [Fact]
    public void Given_BlankAndCustomSeparators_When_Normalized_Then_DefaultsOrPreservesExactValue()
    {
        var settings = new SettingsDto
        {
            PlayedListSeparator = "   ",
            NowPlayingSeparator = " • "
        };

        SettingsDtoNormalizer.Normalize(settings);

        Assert.Equal(SongTextFormatting.DefaultSeparator, settings.PlayedListSeparator);
        Assert.Equal(" • ", settings.NowPlayingSeparator);
    }

    private static string ToComparableJson(string json) =>
        JsonNode.Parse(json)!.ToJsonString(IndentedJsonOptions);
}
