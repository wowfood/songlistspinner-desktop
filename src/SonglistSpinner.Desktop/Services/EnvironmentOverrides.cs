using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;

namespace SonglistSpinner.Services;

/// <summary>
/// Startup configuration for development and testing, read once from the SONGLISTSPINNER_SSL_* environment
/// variables. A missing or invalid address keeps the production endpoint. The fallback credential is used
/// only while secure storage holds none.
/// </summary>
public sealed record EnvironmentOverrides(
    StreamerSongListApiOptions Api,
    StreamerSongListEventsOptions Events,
    StreamerSongListCredential? FallbackCredential)
{
    public const string ApiBaseUrlVariable = "SONGLISTSPINNER_SSL_API_BASE_URL";
    public const string EventsUrlVariable = "SONGLISTSPINNER_SSL_EVENTS_URL";
    public const string AccessTokenVariable = "SONGLISTSPINNER_SSL_ACCESS_TOKEN";
    public const string TokenTypeVariable = "SONGLISTSPINNER_SSL_TOKEN_TYPE";
    public const string ClientIdVariable = "SONGLISTSPINNER_SSL_CLIENT_ID";

    public static EnvironmentOverrides Read(Func<string, string?> readVariable)
    {
        var apiBaseAddress = Uri.TryCreate(readVariable(ApiBaseUrlVariable), UriKind.Absolute, out var configuredAddress)
            ? configuredAddress
            : StreamerSongListApiOptions.ProductionBaseAddress;
        var eventsEndpoint = Uri.TryCreate(readVariable(EventsUrlVariable), UriKind.Absolute, out var configuredEndpoint)
            ? configuredEndpoint
            : StreamerSongListEventsOptions.ProductionEndpoint;

        var token = readVariable(AccessTokenVariable);
        var fallbackCredential = string.IsNullOrWhiteSpace(token)
            ? null
            : new StreamerSongListCredential(
                SecureStorageStreamerSongListCredentialStore.ParseKind(readVariable(TokenTypeVariable)),
                token,
                readVariable(ClientIdVariable));

        return new EnvironmentOverrides(
            new StreamerSongListApiOptions { BaseAddress = apiBaseAddress },
            new StreamerSongListEventsOptions { Endpoint = eventsEndpoint },
            fallbackCredential);
    }
}
