using SonglistSpinner.Core.PlayedSongs;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using Xunit;
using static SonglistSpinner.Core.Tests.TestSongs;

namespace SonglistSpinner.Core.Tests.PlayedSongs;

public class PlayedSongListTests
{
    // ── CreateText (SpinnerQueueItem overload) ────────────────────────────

    [Fact]
    public void Given_ConfigWithSpecificFields_When_CreateTextForQueueItem_Then_UsesConfigFields()
    {
        var result = PlayedSongList.CreateText(Q(), Cfg(["artist", "requester"]));
        Assert.Equal("Artist: Artist A | Requester: User1", result);
    }

    [Fact]
    public void Given_ConfigWithEmptyFields_When_CreateTextForQueueItem_Then_DefaultsToArtistTitle()
    {
        var cfg = new SpinnerConfig { PlayedList = new SpinnerPlayedListConfig { Fields = [] } };
        var result = PlayedSongList.CreateText(Q(), cfg);
        Assert.Equal("Artist: Artist A | Title: Song One", result);
    }

    [Fact]
    public void Given_ConfigWithDonationField_When_CreateTextForQueueItem_Then_IncludesDonation()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong { Artist = "Band", Title = "Track" },
            Requests = [new SpinnerRequest { Name = "Fan", DonationAmount = 5m }]
        };
        var result = PlayedSongList.CreateText(q, Cfg(["artist", "donation"]));
        Assert.Equal("Artist: Band | Donation: 5", result);
    }

    [Fact]
    public void Given_CustomSeparator_When_CreateTextForQueueItem_Then_UsesIt()
    {
        var result = PlayedSongList.CreateText(
            Q(),
            Cfg(["artist", "title"], separator: " / "));
        Assert.Equal("Artist: Artist A / Title: Song One", result);
    }

    [Fact]
    public void Given_HiddenLabels_When_CreateTextForQueueItem_Then_ReturnsValuesOnly()
    {
        var result = PlayedSongList.CreateText(
            Q(),
            Cfg(["artist", "title"], showLabels: false));
        Assert.Equal("Artist A | Song One", result);
    }

    [Fact]
    public void Given_FieldHeaders_When_CreateTextForQueueItem_Then_OmitsInlineLabels()
    {
        var result = PlayedSongList.CreateText(
            Q(),
            Cfg(["artist", "title"], showLabels: true, showFieldHeaders: true));
        Assert.Equal("Artist A | Song One", result);
    }

    // ── CreateText (PlayHistoryItem overload) ─────────────────────────────

    [Fact]
    public void Given_HistoryItemWithSong_When_CreateTextForHistoryItem_Then_ReturnsArtistAndTitle()
    {
        var result = PlayedSongList.CreateText(H(), Cfg(["artist", "title"]));
        Assert.Equal("Artist: Artist A | Title: Song One", result);
    }

    [Fact]
    public void
        Given_HistoryItemWithEmptyConfigFields_When_CreateTextForHistoryItem_Then_DefaultsToArtistTitle()
    {
        var cfg = new SpinnerConfig { PlayedList = new SpinnerPlayedListConfig { Fields = [] } };
        var result = PlayedSongList.CreateText(H(), cfg);
        Assert.Equal("Artist: Artist A | Title: Song One", result);
    }

    [Fact]
    public void Given_HistoryItemWithRequester_When_CreateTextForHistoryItem_Then_IncludesRequester()
    {
        var h = H(requester: "Fan1");
        var result = PlayedSongList.CreateText(h, Cfg(["artist", "requester"]));
        Assert.Equal("Artist: Artist A | Requester: Fan1", result);
    }

    [Fact]
    public void Given_HistoryItemWithDonation_When_CreateTextForHistoryItem_Then_IncludesDonation()
    {
        var h = H(requester: "Fan1", donationAmount: 5m);
        var result = PlayedSongList.CreateText(h, Cfg(["artist", "donation"]));
        Assert.Equal("Artist: Artist A | Donation: 5", result);
    }

    [Fact]
    public void Given_HistoryItemWithNoDonation_When_CreateTextForHistoryItem_Then_OmitsDonationField()
    {
        var result = PlayedSongList.CreateText(H(), Cfg(["artist", "donation"]));
        Assert.Equal("Artist: Artist A", result);
    }

    [Fact]
    public void Given_CustomSeparator_When_CreateTextForHistoryItem_Then_UsesIt()
    {
        var result = PlayedSongList.CreateText(
            H(),
            Cfg(["artist", "title"], separator: " — "));
        Assert.Equal("Artist: Artist A — Title: Song One", result);
    }

    [Fact]
    public void Given_HiddenLabels_When_CreateTextForHistoryItem_Then_ReturnsValuesOnly()
    {
        var result = PlayedSongList.CreateText(
            H(),
            Cfg(["artist", "title"], showLabels: false));
        Assert.Equal("Artist A | Song One", result);
    }

    [Fact]
    public void Given_HistoryItemWithNullSong_When_CreateTextForHistoryItem_Then_ReturnsUnknownValues()
    {
        var h = new PlayHistoryItem { Song = null };
        var result = PlayedSongList.CreateText(h, Cfg(["artist", "title"]));
        Assert.Equal("Artist: Unknown | Title: Unknown", result);
    }

    // ── CreateTexts ───────────────────────────────────────────────────────

    [Fact]
    public void Given_NumberingDisabled_When_CreateTexts_Then_ReturnsUnnumberedText()
    {
        PlayHistoryItem[] songs = [H(title: "Newest"), H(title: "Oldest")];

        var result = PlayedSongList.CreateTexts(songs, Cfg(["title"]));

        Assert.Equal(["Title: Newest", "Title: Oldest"], result);
    }

    [Fact]
    public void Given_NumberingStartsAtTop_When_CreateTexts_Then_NumbersInDisplayOrder()
    {
        PlayHistoryItem[] songs = [H(title: "Newest"), H(title: "Middle"), H(title: "Oldest")];
        var config = Cfg(
            ["title"],
            showNumbers: true,
            numberingStart: SpinnerSettingValues.PlayedListNumberingStarts.Top);

        var result = PlayedSongList.CreateTexts(songs, config);

        Assert.Equal(["1. Title: Newest", "2. Title: Middle", "3. Title: Oldest"], result);
    }

    [Theory]
    [InlineData(SpinnerSettingValues.PlayedListNumberingStarts.Bottom)]
    [InlineData("invalid")]
    public void Given_NumberingDoesNotStartAtTop_When_CreateTexts_Then_OldestIsOne(
        string numberingStart)
    {
        PlayHistoryItem[] songs = [H(title: "Newest"), H(title: "Middle"), H(title: "Oldest")];
        var config = Cfg(["title"], showNumbers: true, numberingStart: numberingStart);

        var result = PlayedSongList.CreateTexts(songs, config);

        Assert.Equal(["3. Title: Newest", "2. Title: Middle", "1. Title: Oldest"], result);
    }

    [Fact]
    public void Given_EmptyHistory_When_CreateTexts_Then_ReturnsEmptyArray()
    {
        var result = PlayedSongList.CreateTexts(
            Array.Empty<PlayHistoryItem>(),
            Cfg(showNumbers: true));

        Assert.Empty(result);
    }

    [Fact]
    public void Given_SingleItem_When_CreateTexts_Then_NumberIsOne()
    {
        var result = PlayedSongList.CreateTexts(
            new[] { H(title: "Only") },
            Cfg(["title"], showNumbers: true));

        Assert.Equal(["1. Title: Only"], result);
    }

    [Fact]
    public void Given_EquivalentQueueAndHistoryItems_When_Numbered_Then_PreviewAndHistoryTextMatch()
    {
        SpinnerQueueItem[] queueSongs = [Q(title: "Newest"), Q(title: "Oldest")];
        PlayHistoryItem[] historySongs = [H(title: "Newest"), H(title: "Oldest")];
        var config = Cfg(["title"], showNumbers: true);

        var previewResult = PlayedSongList.CreateTexts(queueSongs, config);
        var historyResult = PlayedSongList.CreateTexts(historySongs, config);

        Assert.Equal(historyResult, previewResult);
    }

    // ── CreateFieldTable ──────────────────────────────────────────────────

    [Fact]
    public void Given_ConfiguredFields_When_CreateFieldTable_Then_HeadersAndValuesFollowOrder()
    {
        var config = Cfg(
            ["requester", "title", "artist"],
            showNumbers: true,
            numberingStart: SpinnerSettingValues.PlayedListNumberingStarts.Top,
            separator: " → ",
            showFieldHeaders: true);

        var result = PlayedSongList.CreateFieldTable(
            new[] { Q(artist: "Band", title: "Track", requester: "Fan") },
            config);

        Assert.Equal(["Requester", "Title", "Artist"], result.Headers);
        Assert.Equal(" → ", result.Separator);
        var row = Assert.Single(result.Rows);
        Assert.Equal(1, row.Number);
        Assert.Equal(["Fan", "Track", "Band"], row.Values);
    }

    [Fact]
    public void Given_HistoryFieldWithoutValue_When_CreateFieldTable_Then_KeepsAlignedEmptyCell()
    {
        var result = PlayedSongList.CreateFieldTable(
            new[] { H() },
            Cfg(["artist", "donation"], showFieldHeaders: true));

        Assert.Equal(["Artist", "Donation"], result.Headers);
        Assert.Equal(["Artist A", ""], Assert.Single(result.Rows).Values);
    }

    [Fact]
    public void Given_BottomNumbering_When_CreateFieldTable_Then_NumbersMatchTextListOrder()
    {
        PlayHistoryItem[] songs = [H(title: "Newest"), H(title: "Oldest")];
        var result = PlayedSongList.CreateFieldTable(
            songs,
            Cfg(["title"], showNumbers: true, showFieldHeaders: true));

        Assert.Equal([2, 1], result.Rows.Select(row => row.Number));
    }
}
