using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The built Desktop app, started against the simulator on a test profile, with its WebView driven through the
/// DevTools protocol. Disposing closes the connection and kills the app with its WebView2 processes.
/// </summary>
internal sealed class DesktopApp : IAsyncDisposable
{
    /// <summary>The build configuration whose executable is tested; Release when unset.</summary>
    public const string ConfigurationVariable = "SONGLISTSPINNER_E2E_CONFIGURATION";

    private const string WebView2ArgumentsVariable = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
    private const string AppWebViewArgumentsVariable = "SONGLISTSPINNER_WEBVIEW_ARGS";
    private const string AppUrl = "https://0.0.0.1/";

    /// <summary>How long each launch step (the DevTools port, the app page) may take before the launch fails.</summary>
    private static readonly TimeSpan LaunchLimit = TimeSpan.FromSeconds(60);

    private readonly Process _process;
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private bool _disposed;

    private DesktopApp(Process process, IPlaywright playwright, IBrowser browser, IPage page, int overlayPort)
    {
        _process = process;
        _playwright = playwright;
        _browser = browser;
        Page = page;
        OverlayPort = overlayPort;
    }

    /// <summary>The app's Blazor page (https://0.0.0.1/).</summary>
    public IPage Page { get; }

    public int OverlayPort { get; }

    /// <summary>The overlay's server-sent events, on the host name the overlay server accepts.</summary>
    public Uri OverlayEventsUri => new($"http://localhost:{OverlayPort}/overlay/events");

    public static async Task<DesktopApp> LaunchAsync(
        StreamerSongListSimulator simulator,
        TestProfile profile,
        CancellationToken cancellationToken)
    {
        var overlayPort = FindFreePort();
        var startInfo = new ProcessStartInfo(FindExecutable()) { UseShellExecute = false };
        SetEnvironment(startInfo.Environment, simulator, profile, overlayPort);

        DevToolsPort.ForgetPrevious(profile.Directory);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The app did not start.");
        try
        {
            var devToolsPort = await DevToolsPort.WaitAsync(process, profile.Directory, LaunchLimit, cancellationToken);
            var playwright = await Playwright.CreateAsync();
            try
            {
                var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{devToolsPort}");
                var page = await WaitForAppPageAsync(browser.Contexts[0]);
                return new DesktopApp(process, playwright, browser, page, overlayPort);
            }
            catch
            {
                playwright.Dispose();
                throw;
            }
        }
        catch
        {
            await KillAsync(process);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await _browser.CloseAsync();
        }
        catch (PlaywrightException)
        {
            // The app may already have gone; it is killed next either way.
        }

        _playwright.Dispose();
        await KillAsync(_process);
    }

    private static void SetEnvironment(
        IDictionary<string, string?> environment,
        StreamerSongListSimulator simulator,
        TestProfile profile,
        int overlayPort)
    {
        // The developer's own SONGLISTSPINNER_SSL_* overrides must not leak into the app under test.
        foreach (var name in environment.Keys.Where(name => name.StartsWith("SONGLISTSPINNER_SSL_", StringComparison.OrdinalIgnoreCase)).ToList())
            environment.Remove(name);

        // WebView2 ignores its own variable for this app (see the app's UseWebViewBrowserArguments), so arguments a
        // developer set there are passed on through the app's variable, ahead of the DevTools port.
        var inheritedArguments = environment.TryGetValue(WebView2ArgumentsVariable, out var inherited) ? inherited : null;
        environment.Remove(WebView2ArgumentsVariable);
        environment[AppWebViewArgumentsVariable] = string.IsNullOrWhiteSpace(inheritedArguments)
            ? "--remote-debugging-port=0"
            : $"{inheritedArguments} --remote-debugging-port=0";

        environment["SONGLISTSPINNER_PROFILE_DIR"] = profile.Directory;
        environment["SONGLISTSPINNER_SSL_API_BASE_URL"] = simulator.ApiBaseAddress.ToString();
        environment["SONGLISTSPINNER_SSL_EVENTS_URL"] = simulator.EventsEndpoint.ToString();
        environment["SONGLISTSPINNER_SSL_ACCESS_TOKEN"] = simulator.AccessToken;
        environment["SONGLISTSPINNER_SSL_TOKEN_TYPE"] = "streamer";
        environment["SONGLISTSPINNER_OVERLAY_PORT"] = overlayPort.ToString(CultureInfo.InvariantCulture);
        // An unmapped simulator path answers 404, which the update check reads as "no release", so GitHub is
        // never asked.
        environment["SONGLISTSPINNER_UPDATE_RELEASE_URL"] =
            new Uri(simulator.ApiBaseAddress, "_simulator/no-releases/latest").ToString();
    }

    private static async Task KillAsync(Process process)
    {
        using (process)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    /// <summary>
    /// The WebView's one page, once it shows the app. WebView2 opens that page at about:blank and Blazor navigates
    /// it to the app afterwards, so the page can already exist, at the wrong URL, when the DevTools connection
    /// opens: wait for its navigation, not for a new page.
    /// </summary>
    private static async Task<IPage> WaitForAppPageAsync(IBrowserContext context)
    {
        var page = context.Pages.Count > 0
            ? context.Pages[0]
            : await context.WaitForPageAsync(new BrowserContextWaitForPageOptions { Timeout = (float)LaunchLimit.TotalMilliseconds });
        try
        {
            await page.WaitForURLAsync(IsAppUrl, new PageWaitForURLOptions { Timeout = (float)LaunchLimit.TotalMilliseconds });
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"The WebView did not navigate to {AppUrl} within {LaunchLimit.TotalSeconds} s; it is at {page.Url}.", ex);
        }

        return page;
    }

    private static bool IsAppUrl(string url) => url.StartsWith(AppUrl, StringComparison.Ordinal);

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindExecutable()
    {
        var configuration = Environment.GetEnvironmentVariable(ConfigurationVariable) is { Length: > 0 } configured
            ? configured
            : "Release";
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "SonglistSpinner.Desktop.sln")))
            repository = repository.Parent;
        if (repository is null)
            throw new InvalidOperationException("The end-to-end tests must run from the repository checkout.");

        var output = new DirectoryInfo(
            Path.Combine(repository.FullName, "src", "SonglistSpinner.Desktop", "bin", configuration));
        var executable = output.Exists
            ? output.EnumerateFiles("SonglistSpinner.Desktop.exe", SearchOption.AllDirectories)
                .Where(file => file.DirectoryName!.Contains("net10.0-windows", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        return executable?.FullName ?? throw new InvalidOperationException(
            $"The {configuration} Desktop app is not built. Run scripts/run-e2e.ps1, or " +
            $"dotnet build src/SonglistSpinner.Desktop -c {configuration} first.");
    }
}
