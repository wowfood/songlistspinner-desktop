using SonglistSpinner.Core.PlayedSongs;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>A channel's queue and play history, fetched together by
/// <see cref="StreamerSongListClientQueueExtensions.FetchQueueAndHistoryAsync"/>.</summary>
public sealed record QueueAndHistory(SpinnerQueueSnapshot Queue, PlayHistoryItem[] PlayedSongs)
{
    /// <summary>
    /// The queued songs the wheel may land on under <paramref name="config"/>. The filter takes the config
    /// separately from the fetch, so a caller can apply settings that changed while the fetch was in flight.
    /// </summary>
    public List<SpinnerQueueItem> AvailableSongs(SpinnerConfig config) =>
        SongAvailability.FilterAvailableSongs(Queue.Items, PlayedSongs, config);
}
