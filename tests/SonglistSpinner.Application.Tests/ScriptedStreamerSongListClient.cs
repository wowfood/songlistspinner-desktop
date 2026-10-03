using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Application.Tests;

/// <summary>
/// A StreamerSongList API whose queue fetches follow a script and whose queue changes are recorded.
/// Calls that no test in this project needs throw <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class ScriptedStreamerSongListClient : IStreamerSongListClient
{
    private int _queueFetches;

    /// <summary>Responses for successive queue fetches; an empty queue is returned once these run out.</summary>
    public Queue<Func<CancellationToken, Task<SpinnerQueueSnapshot>>> QueueResponses { get; } = new();

    public TaskCompletionSource QueueFetchStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int QueueFetches => Volatile.Read(ref _queueFetches);

    public PlayHistoryItem[] PlayHistory { get; set; } = [];

    public List<int> MarkedPlayed { get; } = [];

    public List<int> PromotedToNowPlaying { get; } = [];

    public List<int> NowPlayingMarkedPlayedFor { get; } = [];

    public Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _queueFetches);
        QueueFetchStarted.TrySetResult();
        Func<CancellationToken, Task<SpinnerQueueSnapshot>> respond;
        lock (QueueResponses)
        {
            if (!QueueResponses.TryDequeue(out respond!))
                return Task.FromResult(new SpinnerQueueSnapshot());
        }

        return respond(cancellationToken);
    }

    public Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = "week",
        CancellationToken cancellationToken = default) => Task.FromResult(PlayHistory);

    public Task<StreamerSongListStreamer> ResolveStreamerAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task MarkQueueItemAsPlayedAsync(QueueEntryId queueEntryId, CancellationToken cancellationToken = default)
    {
        MarkedPlayed.Add(queueEntryId.Value);
        return Task.CompletedTask;
    }

    public Task MarkNowPlayingAsPlayedAsync(StreamerId streamerId, CancellationToken cancellationToken = default)
    {
        NowPlayingMarkedPlayedFor.Add(streamerId.Value);
        return Task.CompletedTask;
    }

    public Task PromoteQueueItemToNowPlayingAsync(
        QueueEntryId queueEntryId,
        CancellationToken cancellationToken = default)
    {
        PromotedToNowPlaying.Add(queueEntryId.Value);
        return Task.CompletedTask;
    }

    public static SpinnerQueueItem Song(int queueId, int position = 0) => new() { QueueId = queueId, Position = position };

    public static SpinnerQueueSnapshot QueueWith(params int[] queueIds) =>
        new() { Items = [.. queueIds.Select(queueId => Song(queueId))] };
}
