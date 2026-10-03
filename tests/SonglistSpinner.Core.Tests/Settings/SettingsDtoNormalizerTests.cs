using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.Songs;
using Xunit;

namespace SonglistSpinner.Core.Tests.Settings;

public class SettingsDtoNormalizerTests
{
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

        SettingsDtoNormalizer.NormalizeInPlace(settings);

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
            PlayedListFields = """["DONATION","artist","donation","unknown"]""",
            NowPlayingFields = "[]",
            WinnerDialogFields = """["REQUESTER","unknown","Title","requester"]"""
        };

        SettingsDtoNormalizer.NormalizeInPlace(settings);

        Assert.Equal("transparent", settings.BackgroundMode);
        Assert.Equal("youtube", settings.StreamerPlatform);
        Assert.Equal("""["donation","artist"]""", settings.PlayedListFields);
        Assert.Equal(SongFieldNames.DefaultJson, settings.NowPlayingFields);
        Assert.Equal("""["requester","title"]""", settings.WinnerDialogFields);
    }

    [Fact]
    public void Given_LegacySettingsWithoutWinnerFields_When_Normalized_Then_PreservesMigrationSignal()
    {
        var settings = new SettingsDto { WinnerDialogFields = null };

        SettingsDtoNormalizer.NormalizeInPlace(settings);

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

        SettingsDtoNormalizer.NormalizeInPlace(settings);

        Assert.Equal(SongTextFormatting.DefaultSeparator, settings.PlayedListSeparator);
        Assert.Equal(" • ", settings.NowPlayingSeparator);
    }
}
