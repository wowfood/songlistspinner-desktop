using Microsoft.Extensions.Logging;

namespace SonglistSpinner.Services;

public static class DiagnosticLogMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the diagnostic log file in <paramref name="logDirectory"/> (%LOCALAPPDATA%\SonglistSpinner\logs unless a
    /// test profile is in use). One provider instance serves both the logging pipeline and the Settings debug-mode
    /// toggle, which switches the file on and off, so it must not be registered by type a second time. App enables it from the saved settings at startup.
    /// </summary>
    public static MauiAppBuilder AddDiagnosticLog(this MauiAppBuilder builder, string logDirectory)
    {
        var diagnosticLog = new DiagnosticFileLoggerProvider(logDirectory, TimeProvider.System);
        builder.Services.AddSingleton(diagnosticLog);
        builder.Logging.AddProvider(diagnosticLog);
        // The file keeps the app's own debug detail but only warnings and errors from the frameworks.
        builder.Logging.AddFilter<DiagnosticFileLoggerProvider>("SonglistSpinner", LogLevel.Debug);
        builder.Logging.AddFilter<DiagnosticFileLoggerProvider>(null, LogLevel.Warning);
        return builder;
    }
}
