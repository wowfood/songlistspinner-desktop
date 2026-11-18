using System.Diagnostics;
using System.Globalization;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// Finds the DevTools port of the app's WebView. The app starts its browser with <c>--remote-debugging-port=0</c>,
/// so the browser picks a free port and writes it to the first line of <see cref="FileName"/> in its data folder.
/// </summary>
internal static class DevToolsPort
{
    public const string FileName = "DevToolsActivePort";

    /// <summary>Deletes the file a previous launch on the same profile left, which names a closed port.</summary>
    public static void ForgetPrevious(string profileDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(profileDirectory, FileName, SearchOption.AllDirectories))
            File.Delete(file);
    }

    /// <exception cref="DevToolsPortTimeoutException">
    /// The port was not written within <paramref name="limit"/>; the usual cause is that the app started its
    /// browser without the test arguments (it then logs "The WebView2 test environment was not ready").
    /// </exception>
    public static async Task<int> WaitAsync(
        Process app,
        string profileDirectory,
        TimeSpan limit,
        CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(limit);
        try
        {
            return await WaitAsync(app, profileDirectory, bounded.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DevToolsPortTimeoutException(
                $"The app's WebView did not open its DevTools port within {limit.TotalSeconds} s. It may have " +
                "started without --remote-debugging-port; the app logs \"The WebView2 test environment was not " +
                "ready\" when it does.", ex);
        }
    }

    private static async Task<int> WaitAsync(Process app, string profileDirectory, CancellationToken cancellationToken)
    {
        using var changed = new SemaphoreSlim(0);
        using var watcher = new FileSystemWatcher(profileDirectory, FileName);
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
        watcher.Created += (_, _) => changed.Release();
        watcher.Changed += (_, _) => changed.Release();
        watcher.Renamed += (_, _) => changed.Release();
        EventHandler appExited = (_, _) => changed.Release();
        app.EnableRaisingEvents = true;
        app.Exited += appExited;
        watcher.EnableRaisingEvents = true;
        try
        {
            // Checked after the watcher starts, so a file written in between is not missed.
            while (true)
            {
                if (TryRead(profileDirectory, out var port)) return port;
                if (app.HasExited)
                    throw new InvalidOperationException($"The app exited with code {app.ExitCode} before its WebView started.");

                await changed.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            app.Exited -= appExited;
        }
    }

    private static bool TryRead(string profileDirectory, out int port)
    {
        port = 0;
        try
        {
            var file = Directory.EnumerateFiles(profileDirectory, FileName, SearchOption.AllDirectories).FirstOrDefault();
            var firstLine = file is null ? null : File.ReadLines(file).FirstOrDefault();
            return int.TryParse(firstLine, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port > 0;
        }
        catch (IOException)
        {
            // The browser is still writing it; the next change event reads it again.
            return false;
        }
    }
}

/// <summary>The app's WebView did not write its DevTools port in time.</summary>
internal sealed class DevToolsPortTimeoutException(string message, Exception innerException)
    : TimeoutException(message, innerException);
