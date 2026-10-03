using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Services;
using SonglistSpinner.Testing;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Services;

/// <summary>
/// The OBS overlay server over real HTTP on a free localhost port, so a running copy of the app on port 5150 is
/// never touched.
/// </summary>
public class LocalOverlayServerTests
{
    private static readonly TimeSpan WaitLimit = SimulatorClients.WaitLimit;

    [Fact]
    public async Task Given_TheServerIsRunning_When_ABrowserOpensTheRoot_Then_ItIsRedirectedToTheOverlay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();

        using var response = await http.GetAsync($"http://localhost:{server.Port}/", cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/overlay", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Given_TheServerIsRunning_When_OpeningTheOverlayUrl_Then_ServesTheEmbeddedPageUnderItsContentSecurityPolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();

        using var response = await http.GetAsync(server.OverlayUrl, cancellationToken);

        Assert.Equal($"http://localhost:{server.Port}/overlay", server.OverlayUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; " +
            "img-src 'self' data: blob: https: http:; connect-src 'self'; object-src 'none'; base-uri 'none'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal(
            DesktopWebAssets.Read("overlay/Overlay.html"),
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [InlineData("/overlay/SongSpinner.contracts.js", "overlay/SongSpinner.contracts.js", "no-cache")]
    [InlineData("/overlay/SongSpinner.interop.js", "spinner/SongSpinner.interop.js", "no-cache")]
    // The vendored wheel library never changes within a release, so browser sources may keep it.
    [InlineData("/overlay/spin-wheel-iife.js", "spinner/spin-wheel-iife.js", "public, max-age=31536000, immutable")]
    public async Task Given_TheServerIsRunning_When_TheOverlayLoadsAScript_Then_ServesTheEmbeddedScriptWithItsCachePolicy(
        string path,
        string wwwrootPath,
        string cacheControl)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();

        using var response = await http.GetAsync($"http://localhost:{server.Port}{path}", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(cacheControl, response.Headers.CacheControl?.ToString());
        Assert.Equal(DesktopWebAssets.Read(wwwrootPath), await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Given_TheServerIsRunning_When_RequestingAnUnknownPath_Then_ItIsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();

        using var response = await http.GetAsync($"http://localhost:{server.Port}/overlay/secrets.json", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Given_TheServerIsRunning_When_ARequestNamesAnotherHost_Then_ItIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();
        // A page on another site that rebinds its DNS name to 127.0.0.1 still sends its own name as the Host.
        using var request = new HttpRequestMessage(HttpMethod.Get, server.OverlayUrl);
        request.Headers.Host = $"attacker.example:{server.Port}";

        // On Windows http.sys rejects the unregistered host before LocalOverlayServer's own check runs; either
        // way the page is not served.
        using var response = await http.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnOverlayConnectsToTheEventStream_When_TheOverlayStateChanges_Then_ItReceivesTheStateThenTheChangeAsEvents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var server = StartServer(overlay);
        using var http = CreateHttpClient();

        using var response = await http.GetAsync(
            $"http://localhost:{server.Port}/overlay/events",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        using var events = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
        var initialState = await ReadEventAsync(events, cancellationToken);
        var health = server.GetHealth();
        overlay.BroadcastCloseWinner();
        var change = await ReadEventAsync(events, cancellationToken);

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("event: init_state", initialState[0]);
        Assert.StartsWith("data: {", initialState[1]);
        Assert.Equal(new LocalOverlayHealth(LocalOverlayServerState.Running, 1, null), health);
        Assert.Equal(["event: close_winner", "data: {}"], change);
    }

    [Fact]
    public async Task Given_ThePortIsAlreadyInUse_When_Starting_Then_HealthReportsTheFailure()
    {
        var port = FreeLoopbackPort();
        using var occupant = new HttpListener();
        occupant.Prefixes.Add($"http://localhost:{port}/");
        occupant.Start();
        await using var server = new LocalOverlayServer(
            new OverlayStateService(),
            NullLogger<LocalOverlayServer>.Instance,
            port);

        server.Start();

        var health = server.GetHealth();
        Assert.Equal(LocalOverlayServerState.Failed, health.ServerState);
        Assert.False(string.IsNullOrWhiteSpace(health.Error));
    }

    private static LocalOverlayServer StartServer(OverlayStateService overlay)
    {
        var server = new LocalOverlayServer(overlay, NullLogger<LocalOverlayServer>.Instance, FreeLoopbackPort());
        server.Start();
        Assert.Equal(LocalOverlayServerState.Running, server.GetHealth().ServerState);
        return server;
    }

    private static HttpClient CreateHttpClient() =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = WaitLimit };

    /// <summary>A port nothing is listening on now; the server binds it straight after.</summary>
    private static int FreeLoopbackPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>Reads one server-sent event: its lines up to the blank line that ends it.</summary>
    private static async Task<string[]> ReadEventAsync(StreamReader events, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (await events.ReadLineAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken) is { } line)
        {
            if (line.Length == 0) break;
            lines.Add(line);
        }

        return [.. lines];
    }
}
