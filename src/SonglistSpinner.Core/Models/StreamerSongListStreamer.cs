namespace SonglistSpinner.Core.Models;

public sealed record StreamerSongListStreamer(
    int Id,
    IReadOnlyList<StreamerSongListPlatformIdentity> Platforms);

public sealed record StreamerSongListPlatformIdentity(
    string Platform,
    string Username,
    string PlatformId);
