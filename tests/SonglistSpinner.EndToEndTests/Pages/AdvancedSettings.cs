using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>Settings &gt; Advanced: diagnostics, reset all settings, and the endpoints in use.</summary>
internal sealed partial class AdvancedSettings(IPage page) : SettingsSection(page, "Advanced")
{
    public ILocator DiagnosticOutput => Checkbox("Enable diagnostic output");

    /// <summary>Opens "Review Settings reset", or "Settings already uses defaults" when nothing differs.</summary>
    public ILocator ReviewResetAllButton =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Review reset all settings" });

    /// <summary>The value shown for <paramref name="term"/>: "Application", "API" or "Local overlay".</summary>
    public ILocator Endpoint(string term) =>
        Page.Locator(".ss-settings-endpoints > div").Filter(new()
        {
            Has = Page.Locator("dt").GetByText(term, new() { Exact = true })
        }).Locator("dd");
}
