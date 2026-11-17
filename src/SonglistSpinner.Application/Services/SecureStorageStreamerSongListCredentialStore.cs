using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// Keeps the StreamerSongList API token in Windows secure storage and its kind and client id in preferences.
/// While none is stored, the environment's fallback credential is used. The fallback itself is never stored:
/// saving it clears the store instead.
/// </summary>
public sealed class SecureStorageStreamerSongListCredentialStore(
    ISecretStore secureStorage,
    IKeyValueStore preferences,
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

        var kind = StreamerSongListCredentialKinds.Parse(
            preferences.GetValue(KindKey) ?? nameof(StreamerSongListCredentialKind.Streamer));
        var clientId = preferences.GetValue(ClientIdKey);
        return new StreamerSongListCredential(kind, token, clientId);
    }

    public async ValueTask SaveCredentialAsync(
        StreamerSongListCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(credential.Token))
            throw new ArgumentException("An API token is required.", nameof(credential));

        if (IsEnvironmentFallback(credential))
        {
            // Settings saves the credential in effect whenever its token field is left blank, and a failed
            // connection test saves the one from before the test. Writing the fallback would keep it after its
            // variable is gone; removing whatever is stored is what puts it back in effect.
            await ClearCredentialAsync(cancellationToken);
            return;
        }

        await secureStorage.SetAsync(TokenKey, credential.Token.Trim());
        preferences.SetValue(KindKey, credential.Kind.ToString());

        if (string.IsNullOrWhiteSpace(credential.ClientId))
            preferences.Remove(ClientIdKey);
        else
            preferences.SetValue(ClientIdKey, credential.ClientId.Trim());
    }

    public ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        secureStorage.Remove(TokenKey);
        preferences.Remove(KindKey);
        preferences.Remove(ClientIdKey);
        return ValueTask.CompletedTask;
    }

    // Compared as the credential would be stored: trimmed, with a blank client id as none.
    private bool IsEnvironmentFallback(StreamerSongListCredential credential) =>
        environment.FallbackCredential is { } fallback
        && credential.Kind == fallback.Kind
        && string.Equals(credential.Token.Trim(), fallback.Token.Trim(), StringComparison.Ordinal)
        && string.Equals(StoredClientId(credential), StoredClientId(fallback), StringComparison.Ordinal);

    private static string? StoredClientId(StreamerSongListCredential credential) =>
        string.IsNullOrWhiteSpace(credential.ClientId) ? null : credential.ClientId.Trim();
}
