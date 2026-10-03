using System.Text.Json;
using System.Text.Json.Nodes;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.Songs;
using Xunit;

namespace SonglistSpinner.Core.Tests.Settings;

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
    public void Given_CustomisedSettingsSavedByDevelop4f36e18_When_ConvertedToConfig_Then_MapsEverySavedValue()
    {
        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Develop4f36e18Customised);

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.True(config.Debug);
        Assert.Equal(["#123456", "#abcdef", "#fedcba"], config.WheelColors);
        Assert.Equal("transparent", config.Background.Mode);
        Assert.Equal("#202020", config.Background.Color);
        Assert.Equal("https://example.com/stage.png", config.Background.Image);
        Assert.Equal("examplestreamer", config.Streamer.DefaultName);
        Assert.Equal("youtube", config.Streamer.Platform);
        Assert.False(config.Streamer.HideChangeOptionWhenDefault);
        Assert.True(config.PlayHistory.ExcludePlayedSongs);
        Assert.Equal("month", config.PlayHistory.Period);
        Assert.Equal(["title", "requester", "donation"], config.PlayedList.Fields);
        Assert.Equal("left", config.PlayedList.Position);
        Assert.Equal("Arial", config.PlayedList.FontFamily);
        Assert.Equal("1rem", config.PlayedList.FontSize);
        Assert.Equal(4, config.PlayedList.MaxLines);
        Assert.True(config.PlayedList.ShowNumbers);
        Assert.Equal("top", config.PlayedList.NumberingStart);
        Assert.Equal(" • ", config.PlayedList.Separator);
        Assert.False(config.PlayedList.ShowLabels);
        Assert.True(config.PlayedList.ShowFieldHeaders);
        Assert.True(config.NowPlaying.Enabled);
        Assert.Equal(["requester", "artist"], config.NowPlaying.Fields);
        Assert.Equal(" / ", config.NowPlaying.Separator);
        Assert.False(config.NowPlaying.ShowLabels);
        Assert.Equal("Georgia, serif", config.NowPlaying.FontFamily);
        Assert.Equal("1.5rem", config.NowPlaying.FontSize);
        Assert.Equal("40rem", config.NowPlaying.Width);
        Assert.Equal("top-right", config.NowPlaying.Position);
        Assert.Equal(["donation", "title"], config.WinnerDialog.Fields);
        Assert.Equal("Verdana", config.WinnerDialog.FontFamily);
        Assert.Equal("1.25rem", config.WinnerDialog.FontSize);
        Assert.Equal("42rem", config.WinnerDialog.Width);
        Assert.False(config.WinnerDialog.ShowQueuePosition);
        Assert.Equal("#eeeeee", config.Colors.Text);
        Assert.Equal("rgba(10, 20, 30, 0.5)", config.Colors.StatusBackground);
        Assert.Equal("#101820", config.Colors.PlayedListBackground);
        Assert.Equal("rgba(16,24,32,0.35)", config.Colors.NowPlayingBackground);
        Assert.Equal("#303030", config.Colors.PlayedItemBackground);
        Assert.Equal("#404040", config.Colors.ResizeHandleBackground);
        Assert.Equal("#606060", config.Colors.ResizeHandleHoverBackground);
        Assert.Equal("#505050", config.Colors.ToggleBackground);
        Assert.Equal("#707070", config.Colors.ButtonBackground);
        Assert.Equal("#000000", config.Colors.ButtonText);
        Assert.Equal("#ff0000", config.Colors.Pointer);
    }

    [Fact]
    public void Given_SettingsSavedByRelease120_When_ConvertedToConfig_Then_WinnerDialogShowsThePlayedListFieldsAndRequester()
    {
        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Release120Customised);

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(["title", "donation", "requester"], config.WinnerDialog.Fields);
    }

    [Fact]
    public void Given_SettingsSavedByRelease120_When_ConvertedToConfig_Then_MapsSavedValuesAndDefaultsTheRest()
    {
        var defaults = new SpinnerConfig();
        var settings = SavedSettingsFixture.Load(SavedSettingsFixture.Release120Customised);

        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.True(config.Debug);
        Assert.Equal(["#123456", "#abcdef"], config.WheelColors);
        Assert.Equal("transparent", config.Background.Mode);
        Assert.Equal("youtube", config.Streamer.Platform);
        Assert.True(config.PlayHistory.ExcludePlayedSongs);
        Assert.Equal("month", config.PlayHistory.Period);
        Assert.Equal(["title", "donation"], config.PlayedList.Fields);
        Assert.Equal("left", config.PlayedList.Position);
        Assert.Equal(4, config.PlayedList.MaxLines);
        Assert.Equal(defaults.PlayedList.Separator, config.PlayedList.Separator);
        Assert.Equal(defaults.PlayedList.ShowLabels, config.PlayedList.ShowLabels);
        Assert.Equal(defaults.PlayedList.ShowFieldHeaders, config.PlayedList.ShowFieldHeaders);
        Assert.Equal(defaults.PlayedList.ShowNumbers, config.PlayedList.ShowNumbers);
        Assert.Equal(defaults.PlayedList.NumberingStart, config.PlayedList.NumberingStart);
        Assert.True(config.NowPlaying.Enabled);
        Assert.Equal(["requester", "artist"], config.NowPlaying.Fields);
        Assert.Equal(defaults.NowPlaying.Separator, config.NowPlaying.Separator);
        Assert.Equal(defaults.NowPlaying.ShowLabels, config.NowPlaying.ShowLabels);
        Assert.Equal("top-right", config.NowPlaying.Position);
        Assert.Equal(defaults.WinnerDialog.FontFamily, config.WinnerDialog.FontFamily);
        Assert.Equal(defaults.WinnerDialog.FontSize, config.WinnerDialog.FontSize);
        Assert.Equal(defaults.WinnerDialog.Width, config.WinnerDialog.Width);
        Assert.Equal(defaults.WinnerDialog.ShowQueuePosition, config.WinnerDialog.ShowQueuePosition);
        // With no saved opacity, the Now Playing panel follows the played-list background.
        Assert.Equal("#101820", config.Colors.NowPlayingBackground);
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
            PlayedListFields = """["title"]""",
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
        Assert.Equal(SpinnerSettingValues.PlayedListPositions.Left, config.PlayedList.Position);
    }

    private static string ToComparableJson(string json) =>
        JsonNode.Parse(json)!.ToJsonString(IndentedJsonOptions);
}
