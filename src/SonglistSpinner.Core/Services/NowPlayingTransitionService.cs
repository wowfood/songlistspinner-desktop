using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

public sealed class NowPlayingTransitionService(ISpinnerApiService apiService)
{
    public async Task PromoteWinnerAsync(
        StreamerSongListChannel channel,
        int streamerId,
        int winnerQueueId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await apiService.FetchQueueSnapshotAsync(channel, cancellationToken);
        if (snapshot.Playing?.QueueId == winnerQueueId) return;

        if (snapshot.Playing is not null)
        {
            await apiService.MarkNowPlayingAsPlayedAsync(streamerId, cancellationToken);
            snapshot = await apiService.FetchQueueSnapshotAsync(channel, cancellationToken);
        }

        if (snapshot.Playing?.QueueId != winnerQueueId)
            await apiService.PromoteQueueItemToNowPlayingAsync(winnerQueueId, cancellationToken);
    }
}
