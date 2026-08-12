using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Contracts;

public interface ISpinnerApiService
{
    Task<SpinnerQueueItem[]> FetchQueueAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default);

    Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = "week",
        CancellationToken cancellationToken = default);

    Task MarkQueueItemAsPlayedAsync(
        int queueId,
        CancellationToken cancellationToken = default);
}
