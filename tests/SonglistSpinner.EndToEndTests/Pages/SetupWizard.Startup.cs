using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class SetupWizard
{
    /// <summary>
    /// A step-2 check ("API and channel", "Realtime events" or "Local OBS overlay"). Its class carries its state:
    /// <c>notrun</c>, <c>running</c>, <c>passed</c> or <c>failed</c>.
    /// </summary>
    public ILocator Check(string check) =>
        page.Locator(".ss-setup-check").Filter(new() { Has = page.Locator("strong").GetByText(check, new() { Exact = true }) });

    /// <summary>Step 3's "name resolved to StreamerSongList channel #id." sentence.</summary>
    public ILocator ResolvedChannel => page.Locator(".ss-setup-complete > p").First;

    /// <summary>Step 3's linked identities, each "Platform username".</summary>
    public ILocator Identities => page.Locator(".ss-setup-identities > span");

    /// <summary>Step 3's notice that an optional check failed.</summary>
    public ILocator Warning => page.Locator(".ss-setup-warning");
}
