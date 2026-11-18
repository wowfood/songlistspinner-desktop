using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// A headless browser standing in for OBS's browser source, so a test can assert what the overlay page renders.
/// It runs the system's Microsoft Edge (Playwright's <c>msedge</c> channel), so no Playwright browser download is
/// needed. Each opened page is a new browser context, like a freshly added browser source.
/// </summary>
internal sealed class OverlayBrowser : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly List<IBrowserContext> _contexts = [];

    private OverlayBrowser(IPlaywright playwright, IBrowser browser)
    {
        _playwright = playwright;
        _browser = browser;
    }

    public static async Task<OverlayBrowser> LaunchAsync()
    {
        var playwright = await Playwright.CreateAsync();
        try
        {
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",
                Headless = true
            });
            return new OverlayBrowser(playwright, browser);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens <paramref name="overlayUri"/> at OBS's usual 1920x1080 canvas, with the wheel probe installed before
    /// the page's scripts run.
    /// </summary>
    public async Task<IPage> OpenAsync(Uri overlayUri)
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });
        _contexts.Add(context);
        await WheelProbe.InstallBeforeLoadAsync(context);
        var page = await context.NewPageAsync();
        await page.GotoAsync(overlayUri.ToString());
        return page;
    }

    /// <summary>Closes every page opened so far, which disconnects them from the overlay's event stream.</summary>
    public async Task CloseAllPagesAsync()
    {
        foreach (var context in _contexts)
            await context.CloseAsync();
        _contexts.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _browser.CloseAsync();
        }
        finally
        {
            _playwright.Dispose();
        }
    }
}
