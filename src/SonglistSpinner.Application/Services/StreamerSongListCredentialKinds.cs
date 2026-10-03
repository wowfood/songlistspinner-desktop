using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// Reads a credential kind saved in preferences or set in SONGLISTSPINNER_SSL_TOKEN_TYPE. Saved values are the
/// enum names, so they are part of the persisted credential contract.
/// </summary>
public static class StreamerSongListCredentialKinds
{
    /// <summary>Accepts an enum name in any case or the aliases bearer, oauth and user; anything else is a streamer token.</summary>
    public static StreamerSongListCredentialKind Parse(string? value)
    {
        if (Enum.TryParse<StreamerSongListCredentialKind>(value, true, out var kind)) return kind;
        return value?.Trim().ToLowerInvariant() switch
        {
            "bearer" or "oauth" => StreamerSongListCredentialKind.OAuthBearer,
            "user" => StreamerSongListCredentialKind.User,
            _ => StreamerSongListCredentialKind.Streamer
        };
    }
}
