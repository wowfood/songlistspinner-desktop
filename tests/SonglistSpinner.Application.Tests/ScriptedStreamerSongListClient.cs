using System.Collections.Concurrent;
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

    /// <summary>The period of each play-history fetch, in order.</summary>
    public ConcurrentQueue<string> PlayHistoryPeriods { get; } = new();

    public Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = "week",
        CancellationToken cancellationToken = default)
    {
        PlayHistoryPeriods.Enqueue(period);
        return Task.FromResult(PlayHistory);
    }

    /// <summary>When set, marking a queue entry played fails with this instead of recording it.</summary>
    public Exception? MarkPlayedFailure { get; set; }

    /// <summary>Answers streamer lookups; lookups are not supported while this is unset.</summary>
    public Func<StreamerSongListChannel, Task<StreamerSongListStreamer>>? ResolveStreamer { get; set; }

    public Task<StreamerSongListStreamer> ResolveStreamerAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default) =>
        ResolveStreamer?.Invoke(channel) ?? throw new NotSupportedException();

    public Task MarkQueueItemAsPlayedAsync(QueueEntryId queueEntryId, CancellationToken cancellationToken = default)
    {
        if (MarkPlayedFailure is not null) return Task.FromException(MarkPlayedFailure);

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
