namespace SonglistSpinner.Core.Contracts;

public interface IStreamerSongListCredentialProvider
{
    ValueTask<StreamerSongListCredential?> GetCredentialAsync(
        CancellationToken cancellationToken = default);
}
