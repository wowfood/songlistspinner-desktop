using System.Text.Json;
using System.Text.Json.Nodes;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Data;

public class SettingsDtoConverterTests
{
    private static readonly JsonSerializerOptions OverlayJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    [Fact]
    public void Given_NoSavedSettings_When_ConvertedToConfig_Then_MatchesTheDefaultConfig()
    {
        var expected = JsonSerializer.Serialize(new SpinnerConfig(), OverlayJsonOptions);

        var config = SettingsDtoConverter.ToSpinnerConfig(new SettingsDto());

        Assert.Equal(
            ToComparableJson(expected),
            ToComparableJson(JsonSerializer.Serialize(config, OverlayJsonOptions)));
    }

    [Fact]
    public void Given_UnreadableWheelColors_When_ConvertedToConfig_Then_UsesTheDefaultWheelColors()
    {
        var settings = new SettingsDto { WheelColors = "not json" };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(SpinnerDefaults.CreateWheelColors(), config.WheelColors);
    }

    [Fact]
    public void Given_SavedWheelColors_When_ConvertedToConfig_Then_KeepsThemInOrder()
    {
        var settings = new SettingsDto { WheelColors = """["#010203","red"]""" };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(["#010203", "red"], config.WheelColors);
    }

    [Fact]
    public void Given_LegacySettingsWithoutWinnerFields_When_ConvertedToConfig_Then_WinnerShowsPlayedFieldsAndRequester()
    {
        var settings = new SettingsDto
        {
            SongListFields = """["title"]""",
            WinnerDialogFields = null
        };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal([SongFieldNames.Title, SongFieldNames.Requester], config.WinnerDialog.Fields);
    }

    [Fact]
    public void Given_IndependentNowPlayingOpacity_When_ConvertedToConfig_Then_NowPlayingUsesPlayedListColorAtThatOpacity()
    {
        var settings = new SettingsDto
        {
            ColorPlayedListBackground = "#112233",
            NowPlayingBackgroundOpacity = 0.5
        };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal("#112233", config.Colors.PlayedListBackground);
        Assert.Equal("rgba(17,34,51,0.50)", config.Colors.NowPlayingBackground);
    }

    [Fact]
    public void Given_EmptyNowPlayingFontAndWidth_When_ConvertedToConfig_Then_UsesTheDefaults()
    {
        var settings = new SettingsDto
        {
            NowPlayingFontFamily = "",
            NowPlayingFontSize = "",
            NowPlayingWidth = ""
        };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(
            (SpinnerDefaults.FontFamily, SpinnerDefaults.NowPlaying.FontSize, SpinnerDefaults.NowPlaying.Width),
            (config.NowPlaying.FontFamily, config.NowPlaying.FontSize, config.NowPlaying.Width));
    }

    [Fact]
    public void Given_NonCanonicalSettings_When_ConvertedToConfig_Then_TheCallersSettingsAreNormalized()
    {
        var settings = new SettingsDto { PlayedListPosition = " LEFT " };

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(SpinnerSettingValues.PlayedListPositions.Left, settings.PlayedListPosition);
        Assert.Equal(SpinnerSettingValues.PlayedListPositions.Left, config.SongList.PlayedListPosition);
    }

    private static string ToComparableJson(string json) =>
        JsonNode.Parse(json)!.ToJsonString(IndentedJsonOptions);
}
