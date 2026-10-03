using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class SettingsMessageBox
{
    /// <summary>The dialog's buttons, in the order shown.</summary>
    public ILocator Buttons => Root.Locator(".mud-dialog-actions").GetByRole(AriaRole.Button);

    /// <summary>The section headings a reset review groups its fields under, such as "Played Songs panel".</summary>
    public ILocator ResetSections => Root.Locator(".ss-reset-dialog-fields h4");
}
