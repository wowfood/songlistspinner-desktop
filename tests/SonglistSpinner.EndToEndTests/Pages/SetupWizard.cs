using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The connection wizard (/setup): 1 Access (the token), 2 Channel (the channel and three checks), 3 Ready. It
/// opens on its own when the app has no credential, or from Settings &gt; Connection.
/// </summary>
internal sealed partial class SetupWizard(IPage page)
{
    public ILocator Heading => page.GetByRole(AriaRole.Heading, new() { Name = "Connect SonglistSpinner" });

    /// <summary>The current step's heading: "StreamerSongList access", "Choose your channel" or "SonglistSpinner is ready".</summary>
    public ILocator StepHeading => page.Locator(".ss-setup-card h2");

    /// <summary>The step marked current in the progress list: "Access", "Channel" or "Ready" (after its number).</summary>
    public ILocator CurrentStep => page.Locator(".ss-setup-progress li[aria-current='step']");

    public ILocator Token => page.Locator("#setupToken");

    /// <summary>Under the token: "A credential is already configured. ..." or "The token is stored in ...".</summary>
    public ILocator TokenHint => page.Locator(".ss-setup-field .ss-field-hint").First;

    public ILocator Reference => page.Locator("#setupReference");

    /// <summary>Values: <c>twitch</c>, <c>youtube</c>, <c>kick</c>, <c>none</c>.</summary>
    public ILocator Platform => page.Locator("#setupPlatform");

    public ILocator Error => page.Locator(".ss-setup-error");

    public ILocator ContinueButton => Button("Continue");

    public ILocator ConnectButton => Button("Connect and verify");

    public ILocator ContinueWithWarningsButton => Button("Continue with warnings");

    public ILocator OpenDashboardButton => Button("Open dashboard");

    /// <summary>The message under a step-2 check: "API and channel", "Realtime events" or "Local OBS overlay".</summary>
    public ILocator CheckMessage(string check) =>
        page.Locator(".ss-setup-check").Filter(new() { Has = page.Locator("strong").GetByText(check, new() { Exact = true }) })
            .Locator("small");

    /// <summary>A step-3 summary figure by its caption: "Queued songs", "History items", "Realtime" or "Overlay".</summary>
    public ILocator SummaryValue(string caption) =>
        page.Locator(".ss-setup-summary > div").Filter(new() { Has = page.Locator("span").GetByText(caption, new() { Exact = true }) })
            .Locator("strong");

    /// <summary>The overlay address step 3 shows for the OBS browser source.</summary>
    public ILocator OverlayUrl => page.Locator(".ss-setup-overlay-url code");

    /// <summary>Opens the wizard from Settings &gt; Connection; Settings must be open on that section.</summary>
    public async Task OpenFromSettingsAsync()
    {
        await Button("Open connection wizard").ClickAsync();
        await Expect(Heading).ToBeVisibleAsync();
    }

    /// <summary>Step 1: enters <paramref name="token"/> (blank keeps a configured one) and continues.</summary>
    public async Task EnterTokenAsync(string token)
    {
        await Token.FillAsync(token);
        await ContinueButton.ClickAsync();
    }

    /// <summary>Step 2: enters the channel and starts the checks, without waiting for their outcome.</summary>
    public async Task ConnectAsync(string reference)
    {
        await Reference.FillAsync(reference);
        await ConnectButton.ClickAsync();
    }

    private ILocator Button(string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
