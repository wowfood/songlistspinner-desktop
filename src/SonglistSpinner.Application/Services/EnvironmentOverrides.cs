using System.Globalization;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Core.Updates;

namespace SonglistSpinner.Services;

/// <summary>
/// Startup configuration for development and testing, read once from the SONGLISTSPINNER_* environment
/// variables. A missing or invalid value keeps the production behaviour: the real StreamerSongList and GitHub
/// endpoints, overlay port <see cref="LocalOverlayServer.DefaultPort"/> and the user's own profile. The fallback
/// credential is used only while secure storage holds none.
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
    public const string ProfileDirectoryVariable = "SONGLISTSPINNER_PROFILE_DIR";
    public const string OverlayPortVariable = "SONGLISTSPINNER_OVERLAY_PORT";
    public const string UpdateReleaseUrlVariable = "SONGLISTSPINNER_UPDATE_RELEASE_URL";
    public const string WebViewBrowserArgumentsVariable = "SONGLISTSPINNER_WEBVIEW_ARGS";

    /// <summary>
    /// A test-only folder that holds every value the app saves (settings, the API credential in plain text, logs
    /// and WebView data) instead of the user's profile; null for the normal profile. Only a rooted path is used.
    /// </summary>
    public string? ProfileDirectory { get; init; }

    /// <summary>The overlay server's port, so a second instance under test does not clash with a running one.</summary>
    public int OverlayPort { get; init; } = LocalOverlayServer.DefaultPort;

    /// <summary>Where the update check asks for the latest release; tests point it away from GitHub.</summary>
    public Uri UpdateReleaseEndpoint { get; init; } = GitHubReleaseUpdateChecker.LatestReleaseEndpoint;

    /// <summary>
    /// Browser arguments for the app's WebView (end-to-end tests open the DevTools protocol with
    /// <c>--remote-debugging-port</c>); null unless a test profile is in use, so a variable left in a user's
    /// environment never opens a debugging port on their real profile.
    /// </summary>
    /// <remarks>
    /// The app reads its own variable because WebView2's WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS does not reach this
    /// app's browser; Desktop's UseWebViewBrowserArguments explains why.
    /// </remarks>
    public string? WebViewBrowserArguments { get; init; }

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
                StreamerSongListCredentialKinds.Parse(readVariable(TokenTypeVariable)),
                token,
                readVariable(ClientIdVariable));

        // A relative path would follow the working directory, which differs between launches.
        var profileDirectory = readVariable(ProfileDirectoryVariable) is { } directory && Path.IsPathFullyQualified(directory)
            ? directory
            : null;
        var webViewBrowserArguments = readVariable(WebViewBrowserArgumentsVariable);
        var overlayPort = int.TryParse(readVariable(OverlayPortVariable), NumberStyles.None, CultureInfo.InvariantCulture,
            out var configuredPort) && configuredPort is > 0 and <= ushort.MaxValue
            ? configuredPort
            : LocalOverlayServer.DefaultPort;
        var updateReleaseEndpoint =
            Uri.TryCreate(readVariable(UpdateReleaseUrlVariable), UriKind.Absolute, out var configuredReleaseEndpoint)
                ? configuredReleaseEndpoint
                : GitHubReleaseUpdateChecker.LatestReleaseEndpoint;

        return new EnvironmentOverrides(
            new StreamerSongListApiOptions { BaseAddress = apiBaseAddress },
            new StreamerSongListEventsOptions { Endpoint = eventsEndpoint },
            fallbackCredential)
        {
            ProfileDirectory = profileDirectory,
            OverlayPort = overlayPort,
            UpdateReleaseEndpoint = updateReleaseEndpoint,
            WebViewBrowserArguments = profileDirectory is null || string.IsNullOrWhiteSpace(webViewBrowserArguments)
                ? null
                : webViewBrowserArguments
        };
    }
}
