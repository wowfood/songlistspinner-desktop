using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SonglistSpinner.Services;

/// <summary>
/// Serves the OBS overlay page, its scripts and its event stream over HTTP on localhost. The page and scripts are
/// the Desktop <c>wwwroot</c> files embedded in this assembly.
/// </summary>
public class LocalOverlayServer : IAsyncDisposable
{
    /// <summary>The port OBS browser sources are set up with; changing it breaks every saved overlay URL.</summary>
    public const int DefaultPort = 5150;

    private const string OverlayResourceName = "SonglistSpinner.WebAssets.Overlay.html";
    private const string ContractsResourceName = "SonglistSpinner.WebAssets.SongSpinner.contracts.js";
    private const string SpinWheelResourceName = "SonglistSpinner.WebAssets.spin-wheel-iife.js";
    private const string InteropResourceName = "SonglistSpinner.WebAssets.SongSpinner.interop.js";
    private static readonly Lazy<byte[]> OverlayDocument = new(() =>
        LoadEmbeddedResource(OverlayResourceName, "The embedded overlay document is missing."));
    private static readonly Lazy<byte[]> SpinWheelScript = new(() =>
        LoadEmbeddedResource(SpinWheelResourceName, "The embedded wheel script is missing."));
    private static readonly Lazy<byte[]> ContractsScript = new(() =>
        LoadEmbeddedResource(ContractsResourceName, "The embedded overlay contracts script is missing."));
    private static readonly Lazy<byte[]> InteropScript = new(() =>
        LoadEmbeddedResource(InteropResourceName, "The embedded wheel interop script is missing."));

    private readonly CancellationTokenSource _cts = new();
    private readonly OverlayStateService _overlay;
    private readonly ILogger<LocalOverlayServer> _logger;
    private readonly object _healthGate = new();
    private HttpListener? _listener;
    private LocalOverlayServerState _state = LocalOverlayServerState.Stopped;
    private string? _error;

    /// <param name="port">
    /// <see cref="DefaultPort"/> unless <see cref="EnvironmentOverrides.OverlayPort"/> moves an app under test; tests
    /// pass a free port.
    /// </param>
    public LocalOverlayServer(OverlayStateService overlay, ILogger<LocalOverlayServer> logger, int port = DefaultPort)
    {
        _overlay = overlay;
        _logger = logger;
        Port = port;
        _overlay.ConnectedClientsChanged += OnConnectedClientsChanged;
    }

    /// <summary>Raised when the server's state or its number of connected overlays changes.</summary>
    public event EventHandler? HealthChanged;

    public int Port { get; }

    /// <summary>The address OBS browser sources load.</summary>
    public string OverlayUrl => $"http://localhost:{Port}/overlay";

    public LocalOverlayHealth GetHealth()
    {
        lock (_healthGate)
        {
            return new LocalOverlayHealth(_state, _overlay.ConnectedClientCount, _error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _overlay.ConnectedClientsChanged -= OnConnectedClientsChanged;
        await _cts.CancelAsync();
        try
        {
            _listener?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Closing the local overlay listener failed");
        }

        SetHealth(LocalOverlayServerState.Stopped);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    // Start and Stop are synchronous: HttpListener starts and stops without blocking on I/O, and
    // requests are accepted on a background loop whose faults are logged.
    public void Start()
    {
        SetHealth(LocalOverlayServerState.Starting);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        try
        {
            _listener.Start();
            SetHealth(LocalOverlayServerState.Running);
            _logger.LogInformation("Local overlay server listening on port {Port}", Port);
            AcceptConnectionsAsync(_cts.Token)
                .ObserveFaults(ex => _logger.LogError(ex, "The local overlay request loop failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local overlay server failed to start on port {Port}", Port);
            SetHealth(LocalOverlayServerState.Failed, ex.Message);
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException)
        {
            // DisposeAsync already closed the listener, so there is nothing left to stop.
        }

        SetHealth(LocalOverlayServerState.Stopped);
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        string? failure = null;
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException ex)
            {
                if (!ct.IsCancellationRequested) failure = ex.Message;
                break;
            }
            catch (ObjectDisposedException ex)
            {
                if (!ct.IsCancellationRequested) failure = ex.Message;
                break;
            }

            Task.Run(() => ServeOverlayRequestAsync(context, ct), CancellationToken.None)
                .ObserveFaults(ex => _logger.LogError(ex, "Serving a local overlay request failed"));
        }

        if (!ct.IsCancellationRequested)
        {
            SetHealth(
                LocalOverlayServerState.Failed,
                failure ?? "The local overlay server stopped unexpectedly.");
        }
    }

    private async Task ServeOverlayRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
        try
        {
            if (!IsAllowedHost(context.Request.Url?.Host))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
                return;
            }

            switch (path)
            {
                case "" or "/":
                    context.Response.Redirect("/overlay");
                    context.Response.Close();
                    break;
                case "/overlay":
                    await ServeHtmlAsync(context);
                    break;
                case "/overlay/events":
                    await ServeSSEAsync(context, ct);
                    break;
                case "/overlay/SongSpinner.contracts.js":
                    await ServeScriptAsync(context, ContractsScript.Value, "no-cache");
                    break;
                case "/overlay/SongSpinner.interop.js":
                    await ServeScriptAsync(context, InteropScript.Value, "no-cache");
                    break;
                case "/overlay/spin-wheel-iife.js":
                    await ServeScriptAsync(
                        context,
                        SpinWheelScript.Value,
                        "public, max-age=31536000, immutable");
                    break;
                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    break;
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // The browser source closed the connection mid-response, which OBS does when it reloads.
            _logger.LogDebug(ex, "Overlay client disconnected while serving {Path}", path);
            AbortResponse(context);
        }
        catch
        {
            // Release the connection, then let the request task's fault handler log the failure.
            AbortResponse(context);
            throw;
        }
    }

    private void SetHealth(LocalOverlayServerState state, string? error = null)
    {
        lock (_healthGate)
        {
            _state = state;
            _error = error;
        }

        OnHealthChanged();
    }

    private void OnConnectedClientsChanged(object? sender, EventArgs e) => OnHealthChanged();

    private void OnHealthChanged()
    {
        var handlers = HealthChanged;
        if (handlers is null) return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // One failing observer must not stop the others or the server operation that changed health.
                _logger.LogError(ex, "An overlay health observer failed");
            }
        }
    }
    private static void AbortResponse(HttpListenerContext context)
    {
        try
        {
            context.Response.Abort();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // The connection is already gone, which is all aborting it would achieve.
        }
    }

    private static async Task ServeHtmlAsync(HttpListenerContext context)
    {
        var bytes = OverlayDocument.Value;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.AddHeader(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; " +
            "img-src 'self' data: blob: https: http:; connect-src 'self'; object-src 'none'; base-uri 'none'");
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ServeSSEAsync(HttpListenerContext context, CancellationToken ct)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.AddHeader("Cache-Control", "no-cache");
        context.Response.AddHeader("X-Accel-Buffering", "no");
        context.Response.SendChunked = true;

        await using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true);
        writer.AutoFlush = true;
        try
        {
            await foreach (var overlayEvent in _overlay.SubscribeAsync(ct))
                await writer.WriteAsync(OverlayServerSentEvents.Frame(overlayEvent));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                // The overlay disconnected first, so the stream cannot be ended cleanly and needs no ending.
            }
        }
    }

    private static async Task ServeScriptAsync(
        HttpListenerContext context,
        byte[] bytes,
        string cacheControl)
    {
        context.Response.ContentType = "text/javascript; charset=utf-8";
        context.Response.AddHeader("Cache-Control", cacheControl);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static bool IsAllowedHost(string? host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] LoadEmbeddedResource(string resourceName, string missingResourceMessage)
    {
        using var stream = typeof(LocalOverlayServer).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(missingResourceMessage);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

}
