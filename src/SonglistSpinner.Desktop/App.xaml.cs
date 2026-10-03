using Microsoft.Extensions.Logging;
using SonglistSpinner.Services;

namespace SonglistSpinner;

public partial class App
{
    private readonly LocalOverlayServer _overlayServer;

    public App(
        LocalOverlayServer overlayServer,
        ILocalSettingsService localSettings,
        DiagnosticFileLoggerProvider diagnosticLog,
        ILogger<App> logger)
    {
        _overlayServer = overlayServer;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var webViewData = Path.Combine(localAppData, "SonglistSpinner", "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);

        diagnosticLog.SetEnabled(localSettings.LoadSettings().DebugMode);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.LogCritical(
                args.ExceptionObject as Exception,
                "Unhandled exception (terminating: {IsTerminating})",
                args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => diagnosticLog.Dispose();

        InitializeComponent();
        overlayServer.Start();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "SonglistSpinner" };
        window.Destroying += (_, _) => _overlayServer.Stop();
        return window;
    }
}
