namespace SonglistSpinner.Core.Contracts;

public interface IStreamerSongListCredentialStore : IStreamerSongListCredentialProvider
{
    ValueTask SaveCredentialAsync(
        StreamerSongListCredential credential,
        CancellationToken cancellationToken = default);

    ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default);
}
