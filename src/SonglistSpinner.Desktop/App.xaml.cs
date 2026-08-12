using SonglistSpinner.Services;

namespace SonglistSpinner;

public partial class App
{
    public App(LocalOverlayServer overlayServer)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var webViewData = Path.Combine(localAppData, "SonglistSpinner", "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);

        InitializeComponent();
        _ = overlayServer.StartAsync(CancellationToken.None);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "SonglistSpinner" };
    }
}
