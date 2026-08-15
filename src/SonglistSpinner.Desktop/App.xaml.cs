using System.Diagnostics;
using SonglistSpinner.Services;

namespace SonglistSpinner;

public partial class App
{
    private readonly LocalOverlayServer _overlayServer;

    public App(LocalOverlayServer overlayServer, ILocalSettingsService localSettings)
    {
        _overlayServer = overlayServer;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var webViewData = Path.Combine(localAppData, "SonglistSpinner", "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);

        DiagnosticLog.Configure(localSettings.LoadSettings().DebugMode);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Trace.WriteLine($"[SonglistSpinner] Unhandled exception: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Trace.WriteLine($"[SonglistSpinner] Unobserved task exception: {args.Exception}");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DiagnosticLog.Shutdown();

        InitializeComponent();
        overlayServer.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "SonglistSpinner" };
        window.Destroying += async (_, _) => await _overlayServer.StopAsync(CancellationToken.None);
        return window;
    }
}
