using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
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
        // frame-ancestors: only the app's own pages (the Settings preview) may show the overlay in a frame.
        Assert.Equal(
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; " +
            "img-src 'self' data: blob: https: http:; connect-src 'self'; object-src 'none'; base-uri 'none'; " +
            "frame-ancestors https://0.0.0.1",
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

    [Fact]
    public async Task Given_TheServerIsRunning_When_Stopped_Then_HealthIsStoppedAndThePortIsFreeAgain()
    {
        await using var server = StartServer(new OverlayStateService());

        server.Stop();

        Assert.Equal(new LocalOverlayHealth(LocalOverlayServerState.Stopped, 0, null), server.GetHealth());
        using var nextOwner = new HttpListener();
        nextOwner.Prefixes.Add($"http://localhost:{server.Port}/");
        nextOwner.Start();
        Assert.True(nextOwner.IsListening);
    }

    [Fact]
    public async Task Given_AnOverlayIsConnectedToTheEventStream_When_TheServerStops_Then_TheStreamEndsAndNoOverlayIsCounted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = StartServer(new OverlayStateService());
        using var http = CreateHttpClient();
        using var response = await http.GetAsync(
            $"http://localhost:{server.Port}/overlay/events",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        using var events = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
        await ReadEventAsync(events, cancellationToken);
        var stoppedWithNoOverlay = WaitForHealth(
            server,
            new LocalOverlayHealth(LocalOverlayServerState.Stopped, 0, null));

        server.Stop();

        await stoppedWithNoOverlay.WaitAsync(WaitLimit, cancellationToken);
        await ReadToTheEndOfTheStreamAsync(events, cancellationToken);
    }

    [Fact]
    public async Task Given_AnOverlayIsConnectedToTheEventStream_When_ItDisconnects_Then_ALaterChangeStopsCountingIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var server = StartServer(overlay);
        var noOverlayConnected = WaitForHealth(
            server,
            new LocalOverlayHealth(LocalOverlayServerState.Running, 0, null));
        using (var http = CreateHttpClient())
        using (var response = await http.GetAsync(
                   $"http://localhost:{server.Port}/overlay/events",
                   HttpCompletionOption.ResponseHeadersRead,
                   cancellationToken))
        using (var events = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken)))
        {
            await ReadEventAsync(events, cancellationToken);
            Assert.Equal(1, server.GetHealth().ConnectedClients);
        }

        // The server only learns that an overlay has gone when it next writes to it, and the first write after
        // the connection closed can still be accepted, so keep changing the state until the server notices. A
        // server that never notices ends the test at the wait limit instead of hanging the run.
        using var waitLimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitLimit.CancelAfter(WaitLimit);
        while (!noOverlayConnected.IsCompleted)
        {
            waitLimit.Token.ThrowIfCancellationRequested();
            overlay.BroadcastCloseWinner();
            await Task.Yield();
        }

        Assert.Equal(0, overlay.ConnectedClientCount);
    }

    [Fact]
    public async Task Given_AHealthObserverThrows_When_TheServerStarts_Then_LaterObserversStillSeeItRunning()
    {
        await using var server = new LocalOverlayServer(
            new OverlayStateService(),
            NullLogger<LocalOverlayServer>.Instance,
            FreeLoopbackPort());
        var observedStates = new List<LocalOverlayServerState>();
        server.HealthChanged += (_, _) => throw new InvalidOperationException("Observer failed");
        server.HealthChanged += (_, _) => observedStates.Add(server.GetHealth().ServerState);

        server.Start();

        Assert.Equal([LocalOverlayServerState.Starting, LocalOverlayServerState.Running], observedStates);
    }

    [Fact]
    public async Task Given_TheServerIsRunning_When_ItsListenerFailsUnderneathIt_Then_HealthReportsTheFailureAndItIsLogged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var logger = new RecordingLogger();
        await using var server = new LocalOverlayServer(new OverlayStateService(), logger, FreeLoopbackPort());
        server.Start();
        var failed = WaitForHealth(server, health => health.ServerState == LocalOverlayServerState.Failed);

        CloseTheListenerUnderneath(server);
        await failed.WaitAsync(WaitLimit, cancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(server.GetHealth().Error));
        var entry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(
            $"The local overlay server stopped accepting connections on port {server.Port}",
            entry.Message);
        Assert.NotNull(entry.Exception);
    }

    /// <summary>Completes when the server reports <paramref name="expected"/>, however many changes that takes.</summary>
    private static Task WaitForHealth(LocalOverlayServer server, LocalOverlayHealth expected) =>
        WaitForHealth(server, health => health == expected);

    private static Task WaitForHealth(LocalOverlayServer server, Func<LocalOverlayHealth, bool> isExpected)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.HealthChanged += (_, _) =>
        {
            if (isExpected(server.GetHealth())) reached.TrySetResult();
        };
        return reached.Task;
    }

    /// <summary>
    /// Closes the server's HttpListener without telling the server, which is how a failure inside HTTP.sys looks
    /// to its accept loop. Nothing reachable from outside the class makes a listener fail, so this reaches the
    /// private field; the test asserts only what the server then reports.
    /// </summary>
    private static void CloseTheListenerUnderneath(LocalOverlayServer server)
    {
        var field = typeof(LocalOverlayServer).GetField("_listener", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("LocalOverlayServer no longer keeps its listener in _listener.");
        var listener = (HttpListener?)field.GetValue(server)
                       ?? throw new InvalidOperationException("The server has no listener; it was not started.");
        listener.Close();
    }

    private sealed class RecordingLogger : ILogger<LocalOverlayServer>
    {
        private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
        {
            get
            {
                lock (_entries)
                    return [.. _entries];
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
                _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>
    /// Returns once the event stream has no more to read, whether it closed cleanly or was cut off. A stream
    /// that stays open fails the test with a timeout.
    /// </summary>
    private static async Task ReadToTheEndOfTheStreamAsync(StreamReader events, CancellationToken cancellationToken)
    {
        try
        {
            while (await events.ReadLineAsync(cancellationToken).AsTask().WaitAsync(WaitLimit, cancellationToken) is not null)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
        }
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
    /// <remarks>
    /// HttpListener cannot bind port 0, so the port is probed and released first. Another process can take it in
    /// between; that window is accepted as a low-probability setup failure (StartServer reports it), not retried.
    /// </remarks>
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
