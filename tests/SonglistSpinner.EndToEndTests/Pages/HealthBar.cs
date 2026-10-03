using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The Dashboard's service-health strip. Each item's state is its bold label (API and Realtime show "Not
/// connected", "Checking", "Connected", "Reconnecting" or "Error"; Overlay shows "Ready", "1 connected" and so
/// on) and its detail is its tooltip.
/// </summary>
internal sealed partial class HealthBar(IPage page)
{
    /// <summary>The API state, such as "Connected".</summary>
    public ILocator Api => Item("API: ").Locator("strong");

    public ILocator Realtime => Item("Realtime: ").Locator("strong");

    public ILocator Overlay => Item("Overlay: ").Locator("strong");

    /// <summary>The loaded channel's name, or "Not loaded".</summary>
    public ILocator Channel => Item("Channel: ").Locator("strong");

    /// <summary>"Custom API" against the simulator (its host is neither production nor staging).</summary>
    public ILocator Environment => page.Locator(".dashboard-health-environment");

    public Task ExpectApiDetailAsync(string detail) => Expect(Item("API: ")).ToHaveAttributeAsync("title", detail);

    public Task ExpectRealtimeDetailAsync(string detail) =>
        Expect(Item("Realtime: ")).ToHaveAttributeAsync("title", detail);

    public Task ExpectOverlayDetailAsync(string detail) =>
        Expect(Item("Overlay: ")).ToHaveAttributeAsync("title", detail);

    private ILocator Item(string ariaLabelPrefix) =>
        page.Locator($".dashboard-health-item[aria-label^='{ariaLabelPrefix}']");
}
