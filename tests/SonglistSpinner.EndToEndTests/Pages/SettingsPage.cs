using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The Settings page: its section navigation, the draft state, Save, and the prompts it raises. Each section is
/// its own page object; opening one selects it in the navigation.
/// </summary>
internal sealed partial class SettingsPage(IPage page)
{
    public IPage Page { get; } = page;

    public ConnectionSettings Connection => new(Page);

    public SpinnerSettings Spinner => new(Page);

    public OverlayLayoutSettings OverlayLayout => new(Page);

    public AppearanceSettings Appearance => new(Page);

    public AdvancedSettings Advanced => new(Page);

    public SettingsPreview Preview => new(Page.FrameLocator("#settingsOverlayPreview"));

    /// <summary>"Unsaved draft" or "All changes saved".</summary>
    public ILocator DraftState => Page.Locator(".ss-settings-draft-state");

    /// <summary>"✓ Saved", "✗ reason", or the unsaved-changes hint, after Save Settings.</summary>
    public ILocator SaveBarState => Page.Locator(".ss-save-bar-state");

    public ILocator SaveButton => Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" });

    public async Task OpenAsync()
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Settings", Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>Saves the draft and waits for "✓ Saved".</summary>
    public async Task SaveAsync()
    {
        await SaveButton.ClickAsync();
        await Expect(Page.Locator(".ss-save-success")).ToHaveTextAsync("✓ Saved");
        await Expect(DraftState).ToHaveTextAsync("All changes saved");
    }

    /// <summary>
    /// The prompt a navigation away from an unsaved draft raises, titled "Unsaved settings", with "Save and
    /// leave", "Abandon changes" and "Keep editing".
    /// </summary>
    public SettingsMessageBox UnsavedChangesPrompt => new(Page, "Unsaved settings");

    /// <summary>The MudBlazor message box or dialog titled <paramref name="title"/>.</summary>
    public SettingsMessageBox MessageBox(string title) => new(Page, title);
}
