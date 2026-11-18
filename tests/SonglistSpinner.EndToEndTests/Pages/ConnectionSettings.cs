using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>Settings &gt; Connection: the default channel and the API credential.</summary>
internal sealed partial class ConnectionSettings(IPage page) : SettingsSection(page, "Connection")
{
    public ILocator DefaultStreamerName => Page.Locator("#defaultStreamerName");

    /// <summary>Values: <c>twitch</c>, <c>youtube</c>, <c>kick</c>, <c>none</c> ("StreamerSongList").</summary>
    public ILocator Platform => Page.Locator("#streamerPlatform");

    public ILocator HideChangeWhenDefault => Checkbox("Hide “Change Streamer” when the default channel is set");

    /// <summary>Values: <c>Streamer</c>, <c>User</c>, <c>OAuthBearer</c>.</summary>
    public ILocator CredentialKind => Page.Locator("#credentialKind");

    public ILocator ApiToken => Page.Locator("#apiToken");

    public ILocator OAuthClientId => Page.Locator("#oauthClientId");

    /// <summary>"✓ API credential configured" or "No API credential configured".</summary>
    public ILocator CredentialStatus => Page.Locator(".ss-auth-status");

    public ILocator ClearCredentialButton =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Clear credential", Exact = true });

    public ILocator TestConnectionButton =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Save and test connection" });

    public ILocator OpenWizardButton => Page.GetByRole(AriaRole.Button, new() { Name = "Open connection wizard" });

    /// <summary>The outcome of a connection test or of clearing the credential.</summary>
    public ILocator Result => Page.Locator(".ss-settings-result");
}
