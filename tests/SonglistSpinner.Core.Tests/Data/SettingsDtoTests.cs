using System.Text.Json;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Data;

public class SettingsDtoTests
{
    [Fact]
    public void Given_LegacySettingsWithoutNumbering_When_Deserialized_Then_UsesSafeDefaults()
    {
        var settings = JsonSerializer.Deserialize<SettingsDto>("{}");

        Assert.NotNull(settings);
        Assert.False(settings.PlayedListShowNumbers);
        Assert.Equal(
            SpinnerSettingValues.PlayedListNumberingStarts.Bottom,
            settings.PlayedListNumberingStart);
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
}
