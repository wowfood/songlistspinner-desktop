using SonglistSpinner.Services;

namespace SonglistSpinner;

public partial class App
{
    public App(LocalOverlayServer overlayServer)
    {
        InitializeComponent();
        _ = overlayServer.StartAsync(CancellationToken.None);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "SonglistSpinner" };
    }
}
