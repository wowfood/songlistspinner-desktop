using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using SonglistSpinner.Services;

namespace SonglistSpinner;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.AddDiagnosticLog();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Clipboard.Default);
        builder.Services.AddSingleton(Launcher.Default);
        // Every HTTP client keeps the 30-second timeout the app used before it had a client factory.
        builder.Services.ConfigureHttpClientDefaults(http =>
            http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30)));

        builder.Services
            .AddLocalSettings()
            .AddStreamerSongList(EnvironmentOverrides.Read(Environment.GetEnvironmentVariable))
            .AddStreamerSession()
            .AddLocalOverlay()
            .AddApplicationUpdates();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
