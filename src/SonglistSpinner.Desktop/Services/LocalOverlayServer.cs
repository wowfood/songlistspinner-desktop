using System.Diagnostics;
using System.Net;
using System.Text;

namespace SonglistSpinner.Services;

public class LocalOverlayServer : IAsyncDisposable
{
    private const string OverlayResourceName = "SonglistSpinner.WebAssets.Overlay.html";
    private const string ContractsResourceName = "SonglistSpinner.WebAssets.SongSpinner.contracts.js";
    private const string SpinWheelResourceName = "SonglistSpinner.WebAssets.spin-wheel-iife.js";
    private static readonly Lazy<byte[]> OverlayDocument = new(() =>
        LoadEmbeddedResource(OverlayResourceName, "The embedded overlay document is missing."));
    private static readonly Lazy<byte[]> SpinWheelScript = new(() =>
        LoadEmbeddedResource(SpinWheelResourceName, "The embedded wheel script is missing."));
    private static readonly Lazy<byte[]> ContractsScript = new(() =>
        LoadEmbeddedResource(ContractsResourceName, "The embedded overlay contracts script is missing."));

    private readonly CancellationTokenSource _cts = new();
    private readonly OverlayStateService _overlay;
    private HttpListener? _listener;

    public LocalOverlayServer(OverlayStateService overlay)
    {
        _overlay = overlay;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            _listener?.Close();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OverlayServer] Listener close failed: {ex}");
        }

        _overlay.SetServerHealth(LocalOverlayServerState.Stopped);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _overlay.SetServerHealth(LocalOverlayServerState.Starting);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_overlay.Port}/");
        try
        {
            _listener.Start();
            _overlay.SetServerHealth(LocalOverlayServerState.Running);
            _ = ProcessRequestsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OverlayServer] Failed to start on port {_overlay.Port}: {ex.Message}");
            _overlay.SetServerHealth(LocalOverlayServerState.Failed, ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException ex) { _ = ex; }

        _overlay.SetServerHealth(LocalOverlayServerState.Stopped);
        return Task.CompletedTask;
    }

    private async Task ProcessRequestsAsync(CancellationToken ct)
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

            _ = Task.Run(() => HandleRequestAsync(context, ct), CancellationToken.None);
        }

        if (!ct.IsCancellationRequested)
        {
            _overlay.SetServerHealth(
                LocalOverlayServerState.Failed,
                failure ?? "The local overlay server stopped unexpectedly.");
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
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
        catch
        {
            try
            {
                context.Response.Abort();
            }
            catch (Exception ex) { _ = ex; }
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
            await foreach (var msg in _overlay.SubscribeAsync(ct))
                await writer.WriteAsync(msg);
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
            catch (Exception ex) { _ = ex; }
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
