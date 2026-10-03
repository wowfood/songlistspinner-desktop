using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Tests;

/// <summary>
/// Builders for the queue entries, played songs and settings the song-text tests share: <c>Q</c> is a queue
/// entry, <c>H</c> a play-history item and <c>Cfg</c> a config with the played-list and winner-dialog fields set.
/// </summary>
internal static class TestSongs
{
    public static SpinnerQueueItem Q(
        int? id = null,
        string artist = "Artist A",
        string title = "Song One",
        string requester = "User1",
        decimal? donation = null,
        int queueId = 0,
        int position = 0)
    {
        return new SpinnerQueueItem
        {
            QueueId = queueId,
            Position = position,
            Song = new SpinnerSong { Id = id, Artist = artist, Title = title },
            Requests = [new SpinnerRequest { Name = requester, DonationAmount = donation }]
        };
    }

    public static PlayHistoryItem H(
        int? id = null,
        string artist = "Artist A",
        string title = "Song One",
        string requester = "",
        decimal? donationAmount = null)
    {
        return new PlayHistoryItem
        {
            Song = new SpinnerSong { Id = id, Artist = artist, Title = title },
            Requests = requester.Length > 0
                ? [new SpinnerRequest { Name = requester, DonationAmount = donationAmount }]
                : []
        };
    }

    public static SpinnerConfig Cfg(
        string[]? fields = null,
        bool exclude = true,
        string[]? winnerFields = null,
        bool showNumbers = false,
        string numberingStart = SpinnerSettingValues.PlayedListNumberingStarts.Bottom,
        string separator = SongTextFormatting.DefaultSeparator,
        bool showLabels = true,
        bool showFieldHeaders = false)
    {
        return new SpinnerConfig
        {
            SongList = new SpinnerSongListConfig
            {
                Fields = fields ?? ["artist", "title"],
                ExcludePlayedSongs = exclude
            },
            PlayedList = new SpinnerPlayedListConfig
            {
                ShowNumbers = showNumbers,
                NumberingStart = numberingStart,
                Separator = separator,
                ShowLabels = showLabels,
                ShowFieldHeaders = showFieldHeaders
            },
            WinnerDialog = new SpinnerWinnerDialogConfig
            {
                Fields = winnerFields ?? ["artist", "title", "requester"]
            }
        };
    }
}
