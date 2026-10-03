using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>What every Settings section page object shares: opening it and finding its subsections.</summary>
/// <param name="page">The app's page.</param>
/// <param name="name">The section's name in the navigation and above its heading, such as "Spinner &amp; Queue".</param>
internal abstract partial class SettingsSection(IPage page, string name)
{
    protected IPage Page { get; } = page;

    /// <summary>Selects the section in the Settings navigation and waits for it to show.</summary>
    public async Task OpenAsync()
    {
        await Page.GetByRole(AriaRole.Navigation, new() { Name = "Settings sections" })
            .GetByRole(AriaRole.Button, new() { Name = name })
            .ClickAsync();
        // The navigation's aria-pressed does not say which section is selected (it renders "" or nothing), so
        // the section's own eyebrow heading does.
        await Expect(Page.Locator(".ss-settings-section-heading > span")).ToHaveTextAsync(name);
    }

    /// <summary>The subsection (a bordered group of fields) whose heading is <paramref name="heading"/>.</summary>
    protected ILocator Subsection(string heading) =>
        Page.Locator("section.ss-settings-section").Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true })
        });

    protected ILocator Checkbox(string label) =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });
}
