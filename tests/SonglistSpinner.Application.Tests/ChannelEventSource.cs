using System.Threading.Channels;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Application.Tests;

/// <summary>A realtime event source that delivers the events a test publishes.</summary>
internal sealed class ChannelEventSource : IStreamerSongListEventSource
{
    private readonly Channel<StreamerSongListEvent> _events = Channel.CreateUnbounded<StreamerSongListEvent>();

    public void Publish(StreamerSongListEventKind kind) =>
        _events.Writer.TryWrite(new StreamerSongListEvent(kind));

    public IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
        StreamerId streamerId,
        CancellationToken cancellationToken = default) => _events.Reader.ReadAllAsync(cancellationToken);
}
