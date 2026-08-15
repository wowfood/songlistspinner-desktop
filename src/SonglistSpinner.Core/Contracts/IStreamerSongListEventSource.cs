namespace SonglistSpinner.Core.Contracts;

public interface IStreamerSongListEventSource
{
    IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
        int streamerId,
        CancellationToken cancellationToken = default);
}

public enum StreamerSongListEventKind
{
    Connected,
    QueueChanged,
    PlayHistoryChanged,
    Reconnecting
}

public sealed record StreamerSongListEvent(
    StreamerSongListEventKind Kind,
    string? EventType = null,
    string? Error = null);
