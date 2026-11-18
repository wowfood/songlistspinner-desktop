namespace SonglistSpinner.Core.StreamerSongList;

public sealed record StreamerSongListCredential(
    StreamerSongListCredentialKind Kind,
    string Token,
    string? ClientId = null);
