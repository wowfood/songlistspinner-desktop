using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The OBS overlay page as a browser source renders it (see <see cref="OverlayBrowser"/>): the wheel, the
/// played-songs panel, the Now Playing panel and the winner reveal, all driven by the app's event stream.
/// </summary>
internal sealed partial class OverlayPage(IPage page)
{
    public IPage Page { get; } = page;

    public PlayedListPanel PlayedList => new(Page);

    /// <summary>The loaded channel's name, or "Waiting for Dashboard..." when none is.</summary>
    public ILocator StreamerLabel => Page.Locator("#streamerLabel");

    public ILocator PlayedCount => Page.Locator("#playedCount");

    public ILocator QueuedCount => Page.Locator("#availableCount");

    /// <summary>Hidden unless the Now Playing workflow is on and something is playing.</summary>
    public ILocator NowPlaying => Page.Locator("#nowPlaying");

    public ILocator NowPlayingText => Page.Locator("#nowPlayingText");

    public ILocator WheelContents => Page.Locator("#wheelContents");

    public ILocator Winner => Page.Locator("#winnerModal");

    /// <summary>The reveal's card, which takes the Winner Dialog Settings' width, font and font size.</summary>
    public ILocator WinnerCard => Winner.Locator(".winner-modal-content");

    public ILocator WinnerLabels => Winner.Locator(".winner-field-label");

    public ILocator WinnerValues => Winner.Locator(".winner-field-value");

    /// <summary>"#2"; hidden when the reveal has no position.</summary>
    public ILocator WinnerQueuePosition => Page.Locator("#winnerQueuePositionValue");

    /// <summary>Waits until the overlay's wheel was last drawn with exactly <paramref name="labels"/>, in order.</summary>
    public Task ExpectWheelLabelsAsync(params string[] labels) => WheelProbe.ExpectLabelsAsync(Page, labels);

    /// <summary>Waits until the winner reveal shows exactly these fields, in order.</summary>
    public async Task ExpectWinnerFieldsAsync(params (string Label, string Value)[] fields)
    {
        await Expect(Winner).ToBeVisibleAsync();
        await Expect(WinnerLabels).ToHaveTextAsync(fields.Select(field => field.Label).ToArray());
        await Expect(WinnerValues).ToHaveTextAsync(fields.Select(field => field.Value).ToArray());
    }

    /// <summary>A CSS custom property the theme sets on the document, such as <c>--app-text-color</c>.</summary>
    public Task<string> ReadThemeVariableAsync(string name) =>
        Page.EvaluateAsync<string>(
            "name => getComputedStyle(document.documentElement).getPropertyValue(name).trim()",
            name);

    /// <summary>The page background's computed colour, such as <c>rgb(17, 17, 17)</c> or <c>rgba(0, 0, 0, 0)</c>.</summary>
    public Task<string> ReadBackgroundColorAsync() =>
        Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");
}
