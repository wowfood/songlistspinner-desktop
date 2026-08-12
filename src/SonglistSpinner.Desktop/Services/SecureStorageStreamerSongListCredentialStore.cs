using SonglistSpinner.Core.Contracts;

namespace SonglistSpinner.Services;

public sealed class SecureStorageStreamerSongListCredentialStore : IStreamerSongListCredentialStore
{
    private const string TokenKey = "streamersonglist_api_token";
    private const string KindKey = "streamersonglist_api_token_kind";
    private const string ClientIdKey = "streamersonglist_api_client_id";

    public async ValueTask<StreamerSongListCredential?> GetCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = await SecureStorage.GetAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            var kind = ParseKind(Preferences.Get(KindKey, nameof(StreamerSongListCredentialKind.Streamer)));
            var clientId = Preferences.Get(ClientIdKey, null);
            return new StreamerSongListCredential(kind, token, clientId);
        }

        token = Environment.GetEnvironmentVariable("SONGLISTSPINNER_SSL_ACCESS_TOKEN");
        if (string.IsNullOrWhiteSpace(token)) return null;

        var environmentKind = Environment.GetEnvironmentVariable("SONGLISTSPINNER_SSL_TOKEN_TYPE");
        var environmentClientId = Environment.GetEnvironmentVariable("SONGLISTSPINNER_SSL_CLIENT_ID");
        return new StreamerSongListCredential(ParseKind(environmentKind), token, environmentClientId);
    }

    public async ValueTask SaveCredentialAsync(
        StreamerSongListCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(credential.Token))
            throw new ArgumentException("An API token is required.", nameof(credential));

        await SecureStorage.SetAsync(TokenKey, credential.Token.Trim());
        Preferences.Set(KindKey, credential.Kind.ToString());

        if (string.IsNullOrWhiteSpace(credential.ClientId))
            Preferences.Remove(ClientIdKey);
        else
            Preferences.Set(ClientIdKey, credential.ClientId.Trim());
    }

    public ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Remove(TokenKey);
        Preferences.Remove(KindKey);
        Preferences.Remove(ClientIdKey);
        return ValueTask.CompletedTask;
    }

    private static StreamerSongListCredentialKind ParseKind(string? value)
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
