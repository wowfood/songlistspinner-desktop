namespace SonglistSpinner.Core.StreamerSongList;

public interface IStreamerSongListCredentialProvider
{
    ValueTask<StreamerSongListCredential?> GetCredentialAsync(
        CancellationToken cancellationToken = default);
}
