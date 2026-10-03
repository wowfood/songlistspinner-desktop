namespace SonglistSpinner.Core.Contracts;

public sealed record StreamerSongListCredential(
    StreamerSongListCredentialKind Kind,
    string Token,
    string? ClientId = null);
