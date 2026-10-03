using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

public sealed class NowPlayingTransitionService(IStreamerSongListClient songListClient)
{
    /// <summary>
    /// Makes the spin's winner the Now Playing entry, first completing whatever is playing now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is not atomic: it is up to two API changes (complete the current Now Playing entry, then promote the
    /// winner) with a queue read before each. If the first change succeeds and the second fails, the previous
    /// song stays in play history and the winner stays in the queue; the failure is an
    /// <see cref="InvalidOperationException"/> that says so, with the API failure as its inner exception.
    /// Cancellation is rethrown as is, even after the first change.
    /// </para>
    /// <para>
    /// Does nothing when the winner is already Now Playing. Throws <see cref="InvalidOperationException"/>
    /// before changing anything when the winner has left the queue.
    /// </para>
    /// </remarks>
    public async Task PromoteWinnerAsync(
        StreamerSongListChannel channel,
        StreamerId streamerId,
        QueueEntryId winner,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await songListClient.FetchQueueSnapshotAsync(channel, cancellationToken);
        if (IsPlaying(snapshot, winner)) return;

        EnsureWinnerAvailable(snapshot, winner);
        var completedCurrentSong = false;
        try
        {
            if (snapshot.Playing is not null)
            {
                await songListClient.MarkNowPlayingAsPlayedAsync(streamerId, cancellationToken);
                completedCurrentSong = true;
                snapshot = await songListClient.FetchQueueSnapshotAsync(channel, cancellationToken);
            }

            if (!IsPlaying(snapshot, winner))
            {
                EnsureWinnerAvailable(snapshot, winner);
                await songListClient.PromoteQueueItemToNowPlayingAsync(winner, cancellationToken);
            }
        }
        catch (Exception ex) when (completedCurrentSong && ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The previous Now Playing song was marked as played, but the winner could not be promoted. " +
                "Refresh the queue before trying again. " + ex.Message, ex);
        }
    }

    private static bool IsPlaying(SpinnerQueueSnapshot snapshot, QueueEntryId winner) =>
        snapshot.Playing?.QueueId == winner.Value;

    private static void EnsureWinnerAvailable(SpinnerQueueSnapshot snapshot, QueueEntryId winner)
    {
        if (!snapshot.Items.Any(item => item.QueueId == winner.Value))
            throw new InvalidOperationException(
                "The selected winner is no longer in the queue. Leave this selection and spin again.");
    }
}
