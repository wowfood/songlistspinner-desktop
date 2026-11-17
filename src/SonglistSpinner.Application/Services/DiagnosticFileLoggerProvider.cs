using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SonglistSpinner.Services;

/// <summary>
/// Writes log entries to <see cref="LogFileName"/> in <see cref="LogDirectory"/> while diagnostic
/// logging (the Settings debug-mode toggle) is enabled, and drops them while it is disabled.
/// </summary>
/// <remarks>
/// A log of <see cref="MaximumLogBytes"/> or more is moved to <see cref="PreviousLogFileName"/>, replacing
/// the older one, both when logging is enabled and when an entry takes the open log to that size, so the
/// folder never holds more than two logs. A log that cannot be moved while it is being written, because
/// another program has it open, keeps growing until logging is next enabled. The file is best-effort: if it
/// cannot be opened or written, logging switches off rather than failing the caller.
/// </remarks>
public sealed class DiagnosticFileLoggerProvider : ILoggerProvider
{
    public const string LogFileName = "songlistspinner.log";
    public const string PreviousLogFileName = "songlistspinner.previous.log";
    public const long MaximumLogBytes = 5 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private StreamWriter? _writer;
    private bool _rotateWhileLogging;

    public DiagnosticFileLoggerProvider(string logDirectory, TimeProvider timeProvider)
    {
        LogDirectory = logDirectory;
        _timeProvider = timeProvider;
    }

    public string LogDirectory { get; }

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
                return _writer is not null;
        }
    }

    public ILogger CreateLogger(string categoryName) => new DiagnosticFileLogger(this, categoryName);

    /// <summary>Opens or closes the log file. Enabling while already enabled keeps the open file.</summary>
    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (enabled == (_writer is not null)) return;
            if (!enabled)
            {
                CloseWriter();
                return;
            }

            try
            {
                OpenLogFile();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The app works without its diagnostic log, so a locked or read-only folder leaves logging off.
                CloseWriter();
                return;
            }

            WriteEntry(LogLevel.Information, typeof(DiagnosticFileLoggerProvider).FullName!,
                "Diagnostic logging enabled.", exception: null);
        }
    }

    public void Dispose()
    {
        lock (_gate)
            CloseWriter();
    }

    private void Write(LogLevel logLevel, string category, string message, Exception? exception)
    {
        lock (_gate)
        {
            if (_writer is null) return;
            WriteEntry(logLevel, category, message, exception);
        }
    }

    // Callers hold _gate and have checked that the writer is open.
    private void WriteEntry(LogLevel logLevel, string category, string message, Exception? exception)
    {
        var entry = new StringBuilder()
            .Append(_timeProvider.GetLocalNow().ToString("O", CultureInfo.InvariantCulture))
            .Append(" [").Append(logLevel).Append("] ")
            .Append(category).Append(": ")
            .Append(message);
        if (exception is not null)
            entry.AppendLine().Append(exception);

        try
        {
            _writer!.WriteLine(entry.ToString());
            // A long run can log a stack trace for every failed retry, so the limit applies while logging too.
            if (_rotateWhileLogging && _writer.BaseStream.Length >= MaximumLogBytes)
                StartNewLogFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A full or failing disk must not break the operation being logged; stop logging instead.
            CloseWriter();
        }
    }

    // Callers hold _gate with the writer closed.
    private void OpenLogFile()
    {
        Directory.CreateDirectory(LogDirectory);
        var logPath = Path.Combine(LogDirectory, LogFileName);
        RotateIfNeeded(logPath);
        _writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
        _rotateWhileLogging = true;
    }

    // Callers hold _gate with the writer open.
    private void StartNewLogFile()
    {
        CloseWriter();
        var logPath = Path.Combine(LogDirectory, LogFileName);
        try
        {
            RotateIfNeeded(logPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log viewer has one of the two files open, which is likely while someone diagnoses a problem.
            // Keep writing to the oversized log rather than lose entries; the next enable moves it aside.
            _rotateWhileLogging = false;
        }

        _writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    private void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaximumLogBytes) return;

        File.Move(logPath, Path.Combine(LogDirectory, PreviousLogFileName), overwrite: true);
    }

    private void CloseWriter()
    {
        if (_writer is null) return;

        try
        {
            _writer.Dispose();
        }
        catch (IOException)
        {
            // Flushing the last entry failed; the file is being abandoned either way.
        }

        _writer = null;
    }

    private sealed class DiagnosticFileLogger : ILogger
    {
        private readonly DiagnosticFileLoggerProvider _provider;
        private readonly string _category;

        public DiagnosticFileLogger(DiagnosticFileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && _provider.IsEnabled;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            _provider.Write(logLevel, _category, formatter(state, exception), exception);
        }
    }
}
