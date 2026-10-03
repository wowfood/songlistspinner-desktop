using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// Keeps the StreamerSongList API token in Windows secure storage and its kind and client id in preferences.
/// While none is stored, the environment's fallback credential is used.
/// </summary>
public sealed class SecureStorageStreamerSongListCredentialStore(
    ISecureStorage secureStorage,
    IPreferences preferences,
    EnvironmentOverrides environment) : IStreamerSongListCredentialStore
{
    private const string TokenKey = "streamersonglist_api_token";
    private const string KindKey = "streamersonglist_api_token_kind";
    private const string ClientIdKey = "streamersonglist_api_client_id";

    public async ValueTask<StreamerSongListCredential?> GetCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = await secureStorage.GetAsync(TokenKey);
        if (string.IsNullOrWhiteSpace(token)) return environment.FallbackCredential;

        var kind = ParseKind(preferences.Get(KindKey, nameof(StreamerSongListCredentialKind.Streamer)));
        var clientId = preferences.Get<string?>(ClientIdKey, null);
        return new StreamerSongListCredential(kind, token, clientId);
    }

    public async ValueTask SaveCredentialAsync(
        StreamerSongListCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(credential.Token))
            throw new ArgumentException("An API token is required.", nameof(credential));

        await secureStorage.SetAsync(TokenKey, credential.Token.Trim());
        preferences.Set(KindKey, credential.Kind.ToString());

        if (string.IsNullOrWhiteSpace(credential.ClientId))
            preferences.Remove(ClientIdKey);
        else
            preferences.Set(ClientIdKey, credential.ClientId.Trim());
    }

    public ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        secureStorage.Remove(TokenKey);
        preferences.Remove(KindKey);
        preferences.Remove(ClientIdKey);
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads a stored or configured credential kind; an unknown value means a streamer token.</summary>
    internal static StreamerSongListCredentialKind ParseKind(string? value)
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
