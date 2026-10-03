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

        EnsureWinnerAvailable(snapshot, winnerQueueId);
        var completedCurrentSong = false;
        try
        {
            if (snapshot.Playing is not null)
            {
                await apiService.MarkNowPlayingAsPlayedAsync(streamerId, cancellationToken);
                completedCurrentSong = true;
                snapshot = await apiService.FetchQueueSnapshotAsync(channel, cancellationToken);
            }

            if (snapshot.Playing?.QueueId != winnerQueueId)
            {
                EnsureWinnerAvailable(snapshot, winnerQueueId);
                await apiService.PromoteQueueItemToNowPlayingAsync(winnerQueueId, cancellationToken);
            }
        }
        catch (Exception ex) when (completedCurrentSong && ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The previous Now Playing song was marked as played, but the winner could not be promoted. " +
                "Refresh the queue before trying again. " + ex.Message, ex);
        }
    }

    private static void EnsureWinnerAvailable(SpinnerQueueSnapshot snapshot, int winnerQueueId)
    {
        if (!snapshot.Items.Any(item => item.QueueId == winnerQueueId))
            throw new InvalidOperationException(
                "The selected winner is no longer in the queue. Leave this selection and spin again.");
    }
}
