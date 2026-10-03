using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// The API credential fields as the Settings page holds them. The token itself is never kept here: only whether
/// one has been entered, because a blank token field means "keep the saved token".
/// </summary>
public sealed record CredentialDraft(StreamerSongListCredentialKind Kind, string ClientId, bool TokenEntered)
{
    /// <summary>
    /// The credential Settings and Setup save from their credential fields. An entered token makes a new credential;
    /// a blank token keeps <paramref name="saved"/>'s token but takes the edited kind and client id. The token and
    /// client id are trimmed, and a blank client id is saved as none.
    /// </summary>
    /// <returns>The credential to save, or <see langword="null"/> when no token was entered and none is saved.</returns>
    public static StreamerSongListCredential? ToCredential(
        StreamerSongListCredentialKind kind,
        string enteredToken,
        string clientId,
        StreamerSongListCredential? saved)
    {
        var token = string.IsNullOrWhiteSpace(enteredToken) ? saved?.Token : enteredToken.Trim();
        if (string.IsNullOrWhiteSpace(token)) return null;

        return new StreamerSongListCredential(
            kind,
            token,
            string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim());
    }
}
