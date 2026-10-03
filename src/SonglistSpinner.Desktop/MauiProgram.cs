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

        var environment = EnvironmentOverrides.Read(Environment.GetEnvironmentVariable);
        var dataDirectory = environment.ProfileDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonglistSpinner");
        // WebView2 reads this when the Blazor WebView starts, which is after the app is built.
        var webViewDataDirectory = Path.Combine(dataDirectory, "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewDataDirectory);
        builder.AddDiagnosticLog(Path.Combine(dataDirectory, "logs"));
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Clipboard.Default);
        builder.Services.AddSingleton(Launcher.Default);
        // Every HTTP client keeps the 30-second timeout the app used before it had a client factory.
        // The typed clients are captured by services that live as long as the app (the update service, and the
        // session services in the WebView's one scope), so the factory never rotates their handler. Recycling
        // pooled connections instead lets those captured clients still follow DNS changes.
        builder.Services.ConfigureHttpClientDefaults(http => http
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }));

        // SONGLISTSPINNER_PROFILE_DIR (end-to-end tests) swaps the MAUI preferences and secure storage for files in
        // that folder, and dataDirectory above moves the logs and WebView data there too, so an app under test
        // never reads or changes the user's own settings, credential or logs. Unset, nothing here changes.
        if (environment.ProfileDirectory is { } profileDirectory)
            builder.Services.AddIsolatedProfile(profileDirectory);
        if (environment.WebViewBrowserArguments is { } webViewBrowserArguments)
            builder.UseWebViewBrowserArguments(webViewDataDirectory, webViewBrowserArguments);

        builder.Services
            .AddLocalSettings()
            .AddStreamerSongList(environment)
            .AddStreamerSession()
            .AddLocalOverlay(environment.OverlayPort)
            .AddApplicationUpdates(environment.UpdateReleaseEndpoint);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
