namespace SonglistSpinner.Core.Contracts;

public sealed record StreamerSongListEvent(
    StreamerSongListEventKind Kind,
    string? EventType = null,
    string? Error = null);
