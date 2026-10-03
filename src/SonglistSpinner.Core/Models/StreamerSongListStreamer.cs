namespace SonglistSpinner.Core.Models;

public sealed record StreamerSongListStreamer(
    StreamerId Id,
    IReadOnlyList<StreamerSongListPlatformIdentity> Platforms);
