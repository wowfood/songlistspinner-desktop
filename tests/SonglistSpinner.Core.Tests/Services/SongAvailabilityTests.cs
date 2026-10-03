using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using Xunit;
using static SonglistSpinner.Core.Tests.TestSongs;

namespace SonglistSpinner.Core.Tests.Services;

public class SongAvailabilityTests
{
    // ── MatchesPlayed ─────────────────────────────────────────────────────

    [Fact]
    public void Given_QueueAndHistoryWithMatchingIds_When_MatchesPlayed_Then_ReturnsTrue()
    {
        Assert.True(SongAvailability.MatchesPlayed(Q(42), H(42)));
    }

    [Fact]
    public void Given_QueueAndHistoryWithDifferentIds_When_MatchesPlayed_Then_ReturnsFalse()
    {
        Assert.False(SongAvailability.MatchesPlayed(Q(1), H(2)));
    }

    [Fact]
    public void Given_SongsWithNoIdsAndMatchingArtistAndTitle_When_MatchesPlayed_Then_ReturnsTrue()
    {
        Assert.True(SongAvailability.MatchesPlayed(Q(), H()));
    }

    [Fact]
    public void Given_SongsWithNoIdsAndDifferentArtist_When_MatchesPlayed_Then_ReturnsFalse()
    {
        Assert.False(SongAvailability.MatchesPlayed(
            Q(artist: "Artist A"), H(artist: "Artist B")));
    }

    [Fact]
    public void Given_SongsWithNoIdsAndDifferentTitle_When_MatchesPlayed_Then_ReturnsFalse()
    {
        Assert.False(SongAvailability.MatchesPlayed(
            Q(title: "Song One"), H(title: "Song Two")));
    }

    [Fact]
    public void Given_SongsWithNoIdsAndArtistTitleDifferByCase_When_MatchesPlayed_Then_ReturnsTrue()
    {
        Assert.True(SongAvailability.MatchesPlayed(
            Q(artist: "ARTIST A", title: "SONG ONE"),
            H(artist: "artist a", title: "song one")));
    }

    [Fact]
    public void Given_QueueItemSongIsNull_When_MatchesPlayed_Then_ReturnsFalse()
    {
        var q = new SpinnerQueueItem { Song = null! };
        Assert.False(SongAvailability.MatchesPlayed(q, H()));
    }

    [Fact]
    public void Given_HistoryItemSongIsNull_When_MatchesPlayed_Then_ReturnsFalse()
    {
        var h = new PlayHistoryItem { Song = null };
        Assert.False(SongAvailability.MatchesPlayed(Q(), h));
    }

    [Fact]
    public void Given_QueueHasIdButHistoryDoesNot_When_MatchesPlayed_Then_FallsBackToArtistTitle()
    {
        Assert.True(SongAvailability.MatchesPlayed(
            Q(5, "Artist A", "Song One"),
            H(null, "Artist A", "Song One")));
    }

    [Fact]
    public void Given_HistoryHasIdButQueueDoesNot_When_MatchesPlayed_Then_FallsBackToArtistTitle()
    {
        Assert.True(SongAvailability.MatchesPlayed(
            Q(null, "Artist A", "Song One"),
            H(5, "Artist A", "Song One")));
    }

    // ── FilterAvailableSongs ──────────────────────────────────────────────

    [Fact]
    public void Given_ExcludeDisabled_When_FilterAvailableSongs_Then_ReturnsAllSongs()
    {
        var all = new List<SpinnerQueueItem> { Q(1), Q(2) };
        var played = new List<PlayHistoryItem> { H(1) };
        var result = SongAvailability.FilterAvailableSongs(all, played, Cfg(exclude: false));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Given_ExcludeEnabledAndSomePlayedById_When_FilterAvailableSongs_Then_ExcludesPlayedSongs()
    {
        var all = new List<SpinnerQueueItem> { Q(1), Q(2) };
        var played = new List<PlayHistoryItem> { H(1) };
        var result = SongAvailability.FilterAvailableSongs(all, played, Cfg(exclude: true));
        Assert.Single(result);
        Assert.Equal(2, result[0].Song.Id);
    }

    [Fact]
    public void Given_ExcludeEnabledAndNoPlayedSongs_When_FilterAvailableSongs_Then_ReturnsAll()
    {
        var all = new List<SpinnerQueueItem> { Q(1), Q(2) };
        var result = SongAvailability.FilterAvailableSongs(all, [], Cfg(exclude: true));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Given_ExcludeEnabledAndAllSongsPlayed_When_FilterAvailableSongs_Then_ReturnsEmpty()
    {
        var all = new List<SpinnerQueueItem> { Q(1), Q(2) };
        var played = new List<PlayHistoryItem> { H(1), H(2) };
        var result = SongAvailability.FilterAvailableSongs(all, played, Cfg(exclude: true));
        Assert.Empty(result);
    }

    [Fact]
    public void Given_ExcludeEnabledAndMatchByArtistTitle_When_FilterAvailableSongs_Then_ExcludesMatch()
    {
        var all = new List<SpinnerQueueItem>
        {
            Q(artist: "X", title: "Y"),
            Q(artist: "A", title: "B")
        };
        var played = new List<PlayHistoryItem> { H(artist: "X", title: "Y") };
        var result = SongAvailability.FilterAvailableSongs(all, played, Cfg(exclude: true));
        Assert.Single(result);
        Assert.Equal("A", result[0].Song.Artist);
    }

    [Fact]
    public void Given_EmptyAllSongs_When_FilterAvailableSongs_Then_ReturnsEmpty()
    {
        var played = new List<PlayHistoryItem> { H(1) };
        var result = SongAvailability.FilterAvailableSongs([], played, Cfg(exclude: true));
        Assert.Empty(result);
    }
}
