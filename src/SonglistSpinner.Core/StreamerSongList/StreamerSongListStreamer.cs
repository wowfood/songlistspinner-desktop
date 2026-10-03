namespace SonglistSpinner.Core.StreamerSongList;

public sealed record StreamerSongListStreamer(
    StreamerId Id,
    IReadOnlyList<StreamerSongListPlatformIdentity> Platforms);
