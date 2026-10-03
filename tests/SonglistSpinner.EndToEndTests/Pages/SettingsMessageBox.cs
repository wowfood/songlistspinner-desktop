using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// A MudBlazor dialog the Settings page opens: the unsaved-changes prompt, "Clear API credential?", a reset
/// review ("Review Played Songs Panel reset") or "... already uses defaults".
/// </summary>
internal sealed class SettingsMessageBox(IPage page, string title)
{
    public ILocator Root => page.Locator(".mud-dialog").Filter(new() { HasText = title });

    public ILocator Title => Root.Locator(".mud-dialog-title");

    /// <summary>The fields a reset review lists, in the order shown.</summary>
    public ILocator ResetFields => Root.Locator(".ss-reset-dialog-fields li");

    public Task ExpectOpenAsync() => Expect(Title).ToHaveTextAsync(title);

    /// <summary>Presses the dialog's button named <paramref name="name"/> and waits for the dialog to close.</summary>
    public async Task ChooseAsync(string name)
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();
        await Expect(Root).ToBeHiddenAsync();
    }
}
