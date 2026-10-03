using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList.Api.V2;

namespace SonglistSpinner.Core.StreamerSongList;

/// <summary>
/// Reads and changes one StreamerSongList channel's queue and play history through API v2.
/// </summary>
/// <remarks>
/// Every call throws <see cref="Api.V2.StreamerSongListApiException"/> when no usable API token is configured,
/// when the API rejects the request, or when it returns a response that does not match API v2. HttpClient's own
/// failures pass through unchanged: <see cref="HttpRequestException"/> when the API cannot be reached and
/// <see cref="TaskCanceledException"/> when the client's timeout expires. Invalid arguments (a blank streamer name, an unsupported platform, an
/// unknown history period or a <c>default</c> id) throw <see cref="ArgumentException"/> before any request is
/// sent. Cancellation throws <see cref="OperationCanceledException"/>.
/// </remarks>
public interface IStreamerSongListClient
{
    Task<StreamerSongListStreamer> ResolveStreamerAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the upcoming queue and the entry that is playing now, in one request.</summary>
    Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the most recently played songs, newest first.</summary>
    /// <param name="channel">The channel whose history to read.</param>
    /// <param name="period">A <see cref="SpinnerSettingValues.PlayHistoryPeriods"/> value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Only the first page is read: at most <see cref="Api.V2.StreamerSongListApiOptions.PageSize"/> items
    /// (100 by default). The response's continuation token is not followed, to keep each refresh to one request,
    /// so a channel that played more songs than that within the period returns only the newest ones, and
    /// excluding played songs from the wheel considers only those. <c>day</c>, <c>week</c> and <c>month</c>
    /// filter by a rolling window ending now; <c>stream</c> and <c>all</c> send no filter, because API v2 has no
    /// stream-scoped filter (see <c>docs/API_V2.md</c>).
    /// </remarks>
    Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = SpinnerSettingValues.PlayHistoryPeriods.Default,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a queue entry straight into play history, without making it Now Playing first.</summary>
    Task MarkQueueItemAsPlayedAsync(
        QueueEntryId queueEntryId,
        CancellationToken cancellationToken = default);

    /// <summary>Completes the streamer's current Now Playing entry, moving it into play history.</summary>
    Task MarkNowPlayingAsPlayedAsync(
        StreamerId streamerId,
        CancellationToken cancellationToken = default);

    /// <summary>Makes a queue entry the Now Playing entry.</summary>
    Task PromoteQueueItemToNowPlayingAsync(
        QueueEntryId queueEntryId,
        CancellationToken cancellationToken = default);
}
