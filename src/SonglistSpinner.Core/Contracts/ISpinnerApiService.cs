using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Contracts;

public interface ISpinnerApiService
{
    Task<StreamerSongListStreamer> ResolveStreamerAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    Task<int> ResolveStreamerIdAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    Task<SpinnerQueueItem[]> FetchQueueAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = SpinnerSettingValues.PlayHistoryPeriods.Default,
        CancellationToken cancellationToken = default);

    Task MarkQueueItemAsPlayedAsync(
        int queueId,
        CancellationToken cancellationToken = default);

    Task MarkNowPlayingAsPlayedAsync(
        int streamerId,
        CancellationToken cancellationToken = default);

    Task PromoteQueueItemToNowPlayingAsync(
        int queueId,
        CancellationToken cancellationToken = default);
}
