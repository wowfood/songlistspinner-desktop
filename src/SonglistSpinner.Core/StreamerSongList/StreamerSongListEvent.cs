namespace SonglistSpinner.Core.StreamerSongList;

public sealed record StreamerSongListEvent(
    StreamerSongListEventKind Kind,
    string? EventType = null,
    string? Error = null);
