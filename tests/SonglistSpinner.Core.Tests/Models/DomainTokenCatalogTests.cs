using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class DomainTokenCatalogTests
{
    [Fact]
    public void Given_DomainCatalogs_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(["artist", "title", "requester", "donation"], SongFieldNames.Values);
        Assert.Equal(["twitch", "youtube", "kick", "none"], StreamerSongListPlatformNames.Values);
        Assert.Equal(["color", "transparent"], SpinnerSettingValues.BackgroundModes.Values);
        Assert.Equal(["right", "left"], SpinnerSettingValues.PlayedListPositions.Values);
        Assert.Equal(
            ["top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right"],
            SpinnerSettingValues.NowPlayingPositions.Values);
        Assert.Equal(["top", "bottom"], SpinnerSettingValues.PlayedListNumberingStarts.Values);
        Assert.Equal(["stream", "day", "week", "month", "all"],
            SpinnerSettingValues.PlayHistoryPeriods.Values);
        Assert.Equal(
            [
                "now_playing_update", "queue_add", "queue_clear", "queue_remove", "queue_reorder", "queue_update"
            ],
            StreamerSongListEventTypes.QueueChanges);
        Assert.Equal(
            ["play_history_add", "play_history_remove"],
            StreamerSongListEventTypes.PlayHistoryChanges);
        Assert.Equal("Bearer", StreamerSongListAuthenticationSchemes.Bearer);
        Assert.Equal("Streamer", StreamerSongListAuthenticationSchemes.Streamer);
        Assert.Equal("User", StreamerSongListAuthenticationSchemes.User);
    }

    [Fact]
    public void Given_DomainCatalogs_When_ComparingValues_Then_EachSetIsCaseInsensitivelyUnique()
    {
        AssertUnique(SongFieldNames.Values);
        AssertUnique(StreamerSongListPlatformNames.Values);
        AssertUnique(SpinnerSettingValues.BackgroundModes.Values);
        AssertUnique(SpinnerSettingValues.PlayedListPositions.Values);
        AssertUnique(SpinnerSettingValues.NowPlayingPositions.Values);
        AssertUnique(SpinnerSettingValues.PlayedListNumberingStarts.Values);
        AssertUnique(SpinnerSettingValues.PlayHistoryPeriods.Values);
        AssertUnique(StreamerSongListEventTypes.QueueChanges.Concat(
            StreamerSongListEventTypes.PlayHistoryChanges));
    }

    [Fact]
    public void Given_MixedFieldSelection_When_Normalizing_Then_OrderIsPreservedAndDuplicatesAreRemoved()
    {
        var result = SongFieldNames.NormalizeSelection(
            [" REQUESTER ", "unknown", "Title", "requester", "DONATION"]);

        Assert.Equal(["requester", "title", "donation"], result);
    }

    [Fact]
    public void Given_NoKnownFields_When_Normalizing_Then_UsesProvidedFallback()
    {
        var result = SongFieldNames.NormalizeSelection(
            ["unknown"],
            SongFieldNames.CreateWinnerDefaultSelection());

        Assert.Equal(["artist", "title", "requester"], result);
    }

    [Fact]
    public void Given_LegacyTransparentSpelling_When_Normalizing_Then_ReturnsCanonicalValue()
    {
        var result = SpinnerSettingValues.BackgroundModes.NormalizeOrDefault(" TRANSPARANT ");

        Assert.Equal("transparent", result);
    }

    [Fact]
    public void Given_MixedCasePlatform_When_Normalizing_Then_ReturnsCanonicalValue()
    {
        var success = StreamerSongListPlatformNames.TryNormalize(" YouTube ", out var result);

        Assert.True(success);
        Assert.Equal("youtube", result);
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var all = values.ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
