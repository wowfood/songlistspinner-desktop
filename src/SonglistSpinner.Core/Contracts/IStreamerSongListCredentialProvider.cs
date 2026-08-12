namespace SonglistSpinner.Core.Contracts;

public interface IStreamerSongListCredentialProvider
{
    ValueTask<StreamerSongListCredential?> GetCredentialAsync(
        CancellationToken cancellationToken = default);
}

public interface IStreamerSongListCredentialStore : IStreamerSongListCredentialProvider
{
    ValueTask SaveCredentialAsync(
        StreamerSongListCredential credential,
        CancellationToken cancellationToken = default);

    ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default);
}

public enum StreamerSongListCredentialKind
{
    OAuthBearer,
    Streamer,
    User
}

public sealed record StreamerSongListCredential(
    StreamerSongListCredentialKind Kind,
    string Token,
    string? ClientId = null);
