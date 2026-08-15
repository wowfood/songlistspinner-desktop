using System.Diagnostics;
using System.Text;

namespace SonglistSpinner.Services;

public static class DiagnosticLog
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private static readonly object Gate = new();
    private static TextWriterTraceListener? _listener;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonglistSpinner",
        "logs");

    public static void Configure(bool enabled)
    {
        lock (Gate)
        {
            RemoveListener();
            if (!enabled) return;

            try
            {
                Directory.CreateDirectory(LogDirectory);
                var logPath = Path.Combine(LogDirectory, "songlistspinner.log");
                RotateIfNeeded(logPath);

                var writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
                _listener = new TextWriterTraceListener(writer, "SonglistSpinnerFile");
                Trace.Listeners.Add(_listener);
                Trace.AutoFlush = true;
                Trace.WriteLine($"[{DateTimeOffset.Now:O}] [SonglistSpinner] Diagnostic logging enabled.");
            }
            catch
            {
                RemoveListener();
            }
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
            RemoveListener();
    }

    private static void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaximumLogBytes) return;

        var previousPath = Path.Combine(LogDirectory, "songlistspinner.previous.log");
        File.Move(logPath, previousPath, overwrite: true);
    }

    private static void RemoveListener()
    {
        if (_listener is null) return;

        Trace.Listeners.Remove(_listener);
        _listener.Flush();
        _listener.Close();
        _listener = null;
    }
}
