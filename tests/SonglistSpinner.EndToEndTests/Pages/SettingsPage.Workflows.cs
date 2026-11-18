using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class SettingsPage
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Settings", Exact = true });

    /// <summary>The name of the section the Settings navigation marks as selected, such as "Appearance".</summary>
    public ILocator SelectedSection =>
        Page.GetByRole(AriaRole.Navigation, new() { Name = "Settings sections" })
            .Locator("button[aria-pressed='true'] strong");

    /// <summary>
    /// Clicks the Dashboard navigation link without waiting for the Dashboard, because an unsaved draft holds the
    /// navigation behind the "Unsaved settings" prompt and a missing credential sends it on to Setup.
    /// </summary>
    public Task ClickDashboardLinkAsync() =>
        Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true }).ClickAsync();
}
