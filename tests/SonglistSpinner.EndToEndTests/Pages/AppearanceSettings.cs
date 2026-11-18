using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// Settings &gt; Appearance: background, wheel palette and overlay colours. The colour pickers are MudBlazor
/// widgets with no stable handles; the background mode, palette and opacities are plain inputs.
/// </summary>
internal sealed partial class AppearanceSettings(IPage page) : SettingsSection(page, "Appearance")
{
    /// <summary>Values: <c>color</c> ("Solid color", the default), <c>transparent</c>.</summary>
    public ILocator BackgroundMode => Page.Locator("#backgroundMode");

    /// <summary>One CSS colour per line.</summary>
    public ILocator WheelColors => Page.Locator("#wheelColors");

    /// <summary>Why Save refused <see cref="WheelColors"/>, naming the first line that is not a CSS colour.</summary>
    public ILocator WheelColorsError => Page.Locator("#wheelColorsError");

    /// <summary>A 0-100 range; fill it with a number.</summary>
    public ILocator PlayedPanelOpacity => Page.Locator("#playedPanelOpacity");

    public ILocator UseSeparateNowPlayingOpacity =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = "Use separate opacity" });

    public ILocator NowPlayingOpacity => Page.Locator("#nowPlayingOpacity");

    /// <summary>"Readability check ..." when the text and panel colours contrast too little.</summary>
    public ILocator ContrastWarning => Page.Locator(".ss-settings-contrast-warning");

    /// <summary>"Background", "Wheel Palette" or "Overlay Colors".</summary>
    public ILocator RevertButton(string subsection) =>
        Page.GetByRole(AriaRole.Button, new() { Name = $"Revert {subsection} to default", Exact = true });
}
