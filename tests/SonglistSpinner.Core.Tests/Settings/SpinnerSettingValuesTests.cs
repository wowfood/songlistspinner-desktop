using System.Text.RegularExpressions;
using SonglistSpinner.Core.Settings;
using Xunit;

namespace SonglistSpinner.Core.Tests.Settings;

public class SpinnerSettingValuesTests
{
    [Fact]
    public void Given_SettingCatalogs_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(["color", "transparent"], SpinnerSettingValues.BackgroundModes.Values);
        Assert.Equal(["right", "left"], SpinnerSettingValues.PlayedListPositions.Values);
        Assert.Equal(
            ["top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right"],
            SpinnerSettingValues.NowPlayingPositions.Values);
        Assert.Equal(["top", "bottom"], SpinnerSettingValues.PlayedListNumberingStarts.Values);
        Assert.Equal(["stream", "day", "week", "month", "all"],
            SpinnerSettingValues.PlayHistoryPeriods.Values);
    }

    [Fact]
    public void Given_SettingCatalogs_When_ComparingValues_Then_EachCatalogIsCaseInsensitivelyUnique()
    {
        Assert.Distinct(SpinnerSettingValues.BackgroundModes.Values, StringComparer.OrdinalIgnoreCase);
        Assert.Distinct(SpinnerSettingValues.PlayedListPositions.Values, StringComparer.OrdinalIgnoreCase);
        Assert.Distinct(SpinnerSettingValues.NowPlayingPositions.Values, StringComparer.OrdinalIgnoreCase);
        Assert.Distinct(SpinnerSettingValues.PlayedListNumberingStarts.Values, StringComparer.OrdinalIgnoreCase);
        Assert.Distinct(SpinnerSettingValues.PlayHistoryPeriods.Values, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_LegacyTransparentSpelling_When_Normalizing_Then_ReturnsCanonicalValue()
    {
        var result = SpinnerSettingValues.BackgroundModes.NormalizeOrDefault(" TRANSPARANT ");

        Assert.Equal("transparent", result);
    }

    // The dashboard and the overlay script read these values from SongSpinner.contracts.js; the tests below
    // fail when the script and SpinnerSettingValues disagree.
    [Fact]
    public void Given_ContractsScript_When_ComparedWithBackgroundModes_Then_TheModesMatch()
    {
        var modes = ReadContractValues("backgroundModes");

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["color"] = SpinnerSettingValues.BackgroundModes.Color,
                ["transparent"] = SpinnerSettingValues.BackgroundModes.Transparent,
                ["legacyTransparent"] = SpinnerSettingValues.BackgroundModes.LegacyTransparent
            },
            modes);
    }

    [Fact]
    public void Given_ContractsScript_When_ComparedWithPlayedListPositions_Then_ThePositionsAndDefaultMatch()
    {
        var positions = ReadContractValues("playedListPositions");

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["left"] = SpinnerSettingValues.PlayedListPositions.Left,
                ["right"] = SpinnerSettingValues.PlayedListPositions.Right,
                ["default"] = SpinnerSettingValues.PlayedListPositions.Default
            },
            positions);
    }

    [Fact]
    public void Given_ContractsScript_When_ComparedWithNowPlayingPositions_Then_ThePositionsAndDefaultMatch()
    {
        var block = ReadContractBlock("nowPlayingPositions");

        var values = Regex.Match(block, @"values: Object\.freeze\(\[(?<list>[^\]]*)\]\)").Groups["list"].Value;
        Assert.Equal(
            SpinnerSettingValues.NowPlayingPositions.Values,
            Regex.Matches(values, "'(?<value>[^']*)'").Select(match => match.Groups["value"].Value));
        Assert.Equal(
            SpinnerSettingValues.NowPlayingPositions.Default,
            Regex.Match(block, "default: '(?<value>[^']*)'").Groups["value"].Value);
    }

    private static Dictionary<string, string> ReadContractValues(string name) =>
        Regex.Matches(ReadContractBlock(name), @"(?<key>\w+): '(?<value>[^']*)'")
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value);

    private static string ReadContractBlock(string name)
    {
        var block = Regex.Match(
            DesktopWebAssets.Read("overlay/SongSpinner.contracts.js"),
            name + @": Object\.freeze\(\{(?<body>[\s\S]*?)\}\)");
        Assert.True(block.Success, $"SongSpinner.contracts.js no longer declares {name}.");
        return block.Groups["body"].Value;
    }
}
