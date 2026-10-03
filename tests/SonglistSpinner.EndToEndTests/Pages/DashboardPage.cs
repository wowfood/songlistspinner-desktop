using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The Dashboard: channel controls, the wheel, the status line and the played-songs panel. Actions wait, through
/// Playwright's retrying expectations, until the page shows they finished. Suites add members in their own
/// partial files.
/// </summary>
internal sealed partial class DashboardPage(IPage page)
{
    public IPage Page { get; } = page;

    public HealthBar Health => new(Page);

    public PlayedListPanel PlayedList => new(Page);

    public WinnerDialog Winner => new(Page);

    public ILocator StreamerInput => Page.Locator("#streamerInput");

    public ILocator StreamerInputError => Page.Locator("#streamerInputError");

    public ILocator LoadButton => Page.GetByRole(AriaRole.Button, new() { Name = "Load", Exact = true });

    /// <summary>"Streamer: name" once a channel is loaded.</summary>
    public ILocator StreamerLabel => Page.Locator("#streamerLabel");

    public ILocator ChangeButton => Page.Locator("#changeStreamerBtn");

    public ILocator RefreshButton => Page.Locator("#refreshSongsBtn");

    public ILocator SpinButton => Page.Locator("#spinButton");

    /// <summary>The played panel's SPIN button, shown only while the wheel is hidden.</summary>
    public ILocator PlayedListSpinButton => Page.Locator("#playedListSpinButton");

    public ILocator ShowWheelCheckbox => Page.Locator("#showWheelCheckbox");

    public ILocator WheelContents => Page.Locator("#wheelContents");

    /// <summary>The status line under the wheel; hidden while it has no message.</summary>
    public ILocator Status => Page.Locator("#status");

    public ILocator QueuedCount => Page.Locator("#availableCount");

    public ILocator PlayedCount => Page.Locator("#playedCount");

    /// <summary>"No eligible queued songs are available to spin." while a loaded channel has none.</summary>
    public ILocator QueueEmptyState => Page.Locator(".queue-empty-state");

    public ILocator NowPlayingSong => Page.Locator("#nowPlayingSong");

    public ILocator MarkNowPlayingPlayedButton => Page.Locator("#markNowPlayingPlayedBtn");

    public ILocator CollapseButton => Page.Locator("#collapseBtn");

    public async Task OpenAsync()
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#container")).ToBeVisibleAsync();
    }

    /// <summary>Enters <paramref name="name"/>, loads it, and waits for the loaded-songs status.</summary>
    public async Task LoadChannelAsync(string name)
    {
        await StartLoadingChannelAsync(name);
        await Expect(StreamerLabel).ToHaveTextAsync($"Streamer: {name}");
        await Expect(Status).ToHaveTextAsync(LoadedStatus());
    }

    /// <summary>Enters <paramref name="name"/> and presses Load without waiting for the outcome.</summary>
    public async Task StartLoadingChannelAsync(string name)
    {
        await StreamerInput.FillAsync(name);
        await LoadButton.ClickAsync();
    }

    /// <summary>Unloads the channel and waits for the empty streamer input.</summary>
    public async Task ChangeChannelAsync()
    {
        await ChangeButton.ClickAsync();
        await Expect(StreamerInput).ToBeVisibleAsync();
        await Expect(Health.Channel).ToHaveTextAsync("Not loaded");
    }

    /// <summary>Presses SPIN and waits for the winner dialog, which a spin opens after about five seconds.</summary>
    public async Task<WinnerDialog> SpinAsync()
    {
        await SpinButton.ClickAsync();
        await Expect(Winner.Root).ToBeVisibleAsync();
        return Winner;
    }

    public async Task SetWheelVisibleAsync(bool visible)
    {
        // The checkbox has no size of its own (the switch draws a slider over it), so press the switch, as a user does.
        if (await ShowWheelCheckbox.IsCheckedAsync() != visible)
            await Page.Locator("label.switch").Filter(new() { Has = ShowWheelCheckbox }).ClickAsync();
        await Expect(ShowWheelCheckbox).ToBeCheckedAsync(new() { Checked = visible });
        if (visible) await Expect(WheelContents).ToBeVisibleAsync();
        else await Expect(WheelContents).ToBeHiddenAsync();
    }

    /// <summary>Marks the Now Playing song played and waits for the Now Playing panel to go.</summary>
    public async Task MarkNowPlayingPlayedAsync()
    {
        await MarkNowPlayingPlayedButton.ClickAsync();
        await Expect(NowPlayingSong).ToBeHiddenAsync();
    }

    /// <summary>
    /// Waits until the wheel was last drawn with exactly <paramref name="labels"/>, in order: the full labels,
    /// before the wheel shortens long ones to fit. See <see cref="WheelProbe"/>.
    /// </summary>
    public Task ExpectWheelLabelsAsync(params string[] labels) => WheelProbe.ExpectLabelsAsync(Page, labels);

    /// <summary>A CSS custom property the theme sets on the document, such as <c>--app-played-list-bg</c>.</summary>
    public Task<string> ReadThemeVariableAsync(string name) =>
        Page.EvaluateAsync<string>(
            "name => getComputedStyle(document.documentElement).getPropertyValue(name).trim()",
            name);

    // Tests that need the count assert the status text themselves; this only says the load finished.
    [GeneratedRegex(@"^Loaded \d+ songs\. Press SPIN!$")]
    private static partial Regex LoadedStatus();
}
