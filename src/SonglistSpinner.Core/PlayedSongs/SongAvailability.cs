using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Core.PlayedSongs;

/// <summary>Decides which queued songs the wheel may land on.</summary>
public static class SongAvailability
{
    /// <summary>
    /// The queue entries the wheel may land on: all of them, or, when the settings exclude played songs, those
    /// that match no song in <paramref name="played"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="played"/> is the play history the app fetched, which is only the first page (100 songs by
    /// default) of the selected period; see <see cref="IStreamerSongListClient.FetchPlayHistoryAsync"/>.
    /// A song played before the oldest one on that page stays on the wheel.
    /// </para>
    /// <para>
    /// O(n×m), which is fine at these sizes (under 200 queued, at most one page played). If that changes, look
    /// the played songs up in a set instead.
    /// </para>
    /// </remarks>
    public static List<SpinnerQueueItem> FilterAvailableSongs(
        IEnumerable<SpinnerQueueItem> all,
        IEnumerable<PlayHistoryItem> played,
        SpinnerConfig config)
    {
        if (!config.PlayHistory.ExcludePlayedSongs) return all.ToList();
        var playedList = played.ToList();
        return all.Where(song => !playedList.Any(p => MatchesPlayed(song, p))).ToList();
    }

    /// <summary>Matches by song id when both sides have one, otherwise by artist and title ignoring case.</summary>
    internal static bool MatchesPlayed(SpinnerQueueItem queueItem, PlayHistoryItem playedItem)
    {
        var q = queueItem.Song;
        var p = playedItem.Song;
        if (q is null || p is null) return false;
        if (q.Id.HasValue && p.Id.HasValue) return q.Id == p.Id;
        return string.Equals(q.Artist, p.Artist, StringComparison.OrdinalIgnoreCase)
               && string.Equals(q.Title, p.Title, StringComparison.OrdinalIgnoreCase);
    }
}
