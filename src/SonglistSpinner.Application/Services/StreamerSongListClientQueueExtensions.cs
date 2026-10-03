using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

public static class StreamerSongListClientQueueExtensions
{
    /// <summary>
    /// Fetches the channel's queue and its play history for <paramref name="playHistoryPeriod"/> in parallel.
    /// Every caller that shows or spins the queue reads it through here, so the two always come from the
    /// same moment.
    /// </summary>
    public static async Task<QueueAndHistory> FetchQueueAndHistoryAsync(
        this IStreamerSongListClient client,
        StreamerSongListChannel channel,
        string playHistoryPeriod,
        CancellationToken cancellationToken)
    {
        var queueTask = client.FetchQueueSnapshotAsync(channel, cancellationToken);
        var historyTask = client.FetchPlayHistoryAsync(channel, playHistoryPeriod, cancellationToken);
        await Task.WhenAll(queueTask, historyTask);
        return new QueueAndHistory(await queueTask, await historyTask);
    }
}
