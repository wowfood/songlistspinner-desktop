using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Contracts;

/// <summary>Realtime notice that a streamer's queue or play history changed.</summary>
public interface IStreamerSongListEventSource
{
    /// <summary>
    /// Streams change notices for one streamer until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequence never completes on its own and never throws for a lost connection. Each connection starts
    /// with <see cref="StreamerSongListEventKind.Connected"/>; when it drops, the source yields
    /// <see cref="StreamerSongListEventKind.Reconnecting"/> with the reason, waits, and connects again. The
    /// wait starts at the configured initial delay and doubles per failed attempt up to the maximum, and it
    /// resets once a connection succeeds.
    /// </para>
    /// <para>
    /// Notices are hints to re-fetch, not the changed data: a consumer should re-read the queue or history.
    /// Notices that arrive while the subscription is still being set up are buffered and yielded straight
    /// after <see cref="StreamerSongListEventKind.Connected"/>. Notices sent while disconnected are lost, so a
    /// consumer should also re-read after <see cref="StreamerSongListEventKind.Connected"/>.
    /// </para>
    /// <para>Cancelling ends the sequence; the consumer may see <see cref="OperationCanceledException"/>.</para>
    /// </remarks>
    IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
        StreamerId streamerId,
        CancellationToken cancellationToken = default);
}
