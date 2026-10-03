using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Application.Tests;

/// <summary>A realtime event source that delivers the events a test publishes.</summary>
internal sealed class ChannelEventSource : IStreamerSongListEventSource
{
    private readonly Channel<StreamerSongListEvent> _events = Channel.CreateUnbounded<StreamerSongListEvent>();
    private readonly TaskCompletionSource _subscriptionEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _failure;

    /// <summary>Completes when a subscriber stops reading, whether it was cancelled or the stream ended.</summary>
    public Task SubscriptionEnded => _subscriptionEnded.Task;

    public void Publish(StreamerSongListEventKind kind, string? error = null) =>
        _events.Writer.TryWrite(new StreamerSongListEvent(kind, Error: error));

    /// <summary>Ends the stream with <paramref name="failure"/> once the events published so far are read.</summary>
    public void Fail(Exception failure)
    {
        _failure = failure;
        _events.Writer.TryComplete();
    }

    public async IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
        StreamerId streamerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var notification in _events.Reader.ReadAllAsync(cancellationToken))
                yield return notification;

            if (_failure is not null) throw _failure;
        }
        finally
        {
            _subscriptionEnded.TrySetResult();
        }
    }
}
