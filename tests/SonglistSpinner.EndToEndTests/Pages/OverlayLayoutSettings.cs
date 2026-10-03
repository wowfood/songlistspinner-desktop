using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// Settings &gt; Overlay Layout: the Played Songs Panel, the Now Playing Panel (its fields show only while the Now
/// Playing workflow is on) and the Winner Dialog Settings. Each subsection has a "Revert to default" button.
/// </summary>
internal sealed partial class OverlayLayoutSettings(IPage page) : SettingsSection(page, "Overlay Layout")
{
    public ILocator PlayedPanel => Subsection("Played Songs Panel");

    public ILocator NowPlayingPanel => Subsection("Now Playing Panel");

    public ILocator WinnerDialogPanel => Subsection("Winner Dialog Settings");

    public FieldOrderEditor PlayedFields => new(PlayedPanel.Locator(".ss-field-multiselect"));

    /// <summary>
    /// Keys: <c>pipe</c>, <c>bullet</c>, <c>middle-dot</c>, <c>diamond</c>, <c>star</c>, <c>slash</c>,
    /// <c>dash</c>, <c>arrow</c>, <c>custom</c> (which shows <see cref="PlayedCustomSeparator"/>).
    /// </summary>
    public ILocator PlayedSeparator => Page.Locator("#playedListSeparatorChoice");

    public ILocator PlayedCustomSeparator => Page.Locator("#playedListSeparator");

    /// <summary>Disabled while the header row is on.</summary>
    public ILocator PlayedShowLabels => PlayedPanel.GetByRole(AriaRole.Checkbox, new() { Name = "Show field labels" });

    public ILocator PlayedShowHeaders => Checkbox("Show field header row");

    public ILocator PlayedShowNumbers => Checkbox("Show sequence numbers");

    /// <summary>Values: <c>top</c> ("Top of list"), <c>bottom</c> ("Bottom of list", the default).</summary>
    public ILocator PlayedNumberingStart => Page.Locator("#playedListNumberingStart");

    /// <summary>Values: <c>right</c> (the default), <c>left</c>.</summary>
    public ILocator PlayedPosition => Page.Locator("#playedListPosition");

    public ILocator PlayedFont => Page.Locator("#playedListFont");

    public ILocator PlayedFontSize => Page.Locator("#playedListFontSize");

    public ILocator PlayedMaxLines => Page.Locator("#playedListMaxLines");

    public FieldOrderEditor NowPlayingFields => new(NowPlayingPanel.Locator(".ss-field-multiselect"));

    public ILocator NowPlayingSeparator => Page.Locator("#nowPlayingSeparatorChoice");

    public ILocator NowPlayingCustomSeparator => Page.Locator("#nowPlayingSeparator");

    public ILocator NowPlayingShowLabels =>
        NowPlayingPanel.GetByRole(AriaRole.Checkbox, new() { Name = "Show field labels" });

    /// <summary>Values: <c>top-left</c> to <c>bottom-right</c>; <c>bottom-left</c> by default.</summary>
    public ILocator NowPlayingPosition => Page.Locator("#nowPlayingPosition");

    public ILocator NowPlayingWidth => Page.Locator("#nowPlayingWidth");

    public ILocator NowPlayingFont => Page.Locator("#nowPlayingFont");

    public ILocator NowPlayingFontSize => Page.Locator("#nowPlayingFontSize");

    public FieldOrderEditor WinnerFields => new(WinnerDialogPanel.Locator(".ss-field-multiselect"));

    public ILocator WinnerWidth => Page.Locator("#winnerDialogWidth");

    public ILocator WinnerFont => Page.Locator("#winnerDialogFont");

    public ILocator WinnerFontSize => Page.Locator("#winnerDialogFontSize");

    public ILocator WinnerShowQueuePosition => Checkbox("Show queue position when available");

    /// <summary>
    /// The "Revert to default" button of <paramref name="subsection"/>: "Played Songs Panel", "Now Playing Panel"
    /// or "Winner Dialog Settings". It opens a review dialog, or "... already uses defaults".
    /// </summary>
    public ILocator RevertButton(string subsection) =>
        Page.GetByRole(AriaRole.Button, new() { Name = $"Revert {subsection} to default", Exact = true });
}
