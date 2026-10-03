using Microsoft.Playwright;
using SonglistSpinner.Simulator;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The user actions the workflows share, written against the app's element ids and accessible names. Each one
/// waits, through Playwright's retrying expectations, for the page to show that the action finished.
/// </summary>
internal static class AppPages
{
    static AppPages()
    {
        // A spin animates for five seconds and the app then fetches the winner's queue position.
        SetDefaultExpectTimeout(20_000);
    }

    public static ILocator AvailableCount(this IPage page) => page.Locator("#availableCount");

    public static ILocator WinnerDialog(this IPage page) => page.Locator("#winnerModal");

    public static async Task LoadChannelAsync(this IPage page, string name)
    {
        await page.Locator("#streamerInput").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Load", Exact = true }).ClickAsync();
        await Expect(page.Locator("#streamerLabel")).ToHaveTextAsync($"Streamer: {name}");
    }

    public static async Task WaitForRealtimeAsync(this IPage page) =>
        await Expect(page.Locator("[aria-label^='Realtime: Connected']")).ToBeVisibleAsync();

    /// <summary>Spins the wheel and returns the queue entry the winner dialog shows.</summary>
    public static async Task<SimulatedQueueEntry> SpinAsync(this IPage page, SimulatedChannel channel)
    {
        await page.Locator("#spinButton").ClickAsync();
        await Expect(page.WinnerDialog()).ToBeVisibleAsync();
        var winnerText = await page.Locator("#winnerFields").InnerTextAsync();
        return channel.Queue.Single(entry =>
            winnerText.Contains(entry.Title, StringComparison.Ordinal) &&
            winnerText.Contains(entry.Artist, StringComparison.Ordinal));
    }

    public static async Task OpenSettingsAsync(this IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Settings", Exact = true })).ToBeVisibleAsync();
    }

    public static async Task OpenDashboardAsync(this IPage page) =>
        await page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true }).ClickAsync();

    public static ILocator NowPlayingWorkflowCheckbox(this IPage page) =>
        page.GetByRole(AriaRole.Checkbox, new() { Name = "Enable Now Playing workflow" });

    /// <summary>From Settings, opens Spinner &amp; Queue, turns the Now Playing workflow on and saves.</summary>
    public static async Task EnableNowPlayingWorkflowAsync(this IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Spinner & Queue" }).ClickAsync();
        await page.NowPlayingWorkflowCheckbox().CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" }).ClickAsync();
        await Expect(page.Locator(".ss-save-success")).ToBeVisibleAsync();
    }
}
