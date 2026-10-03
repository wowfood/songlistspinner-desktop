using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Services;
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

        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(Clipboard.Default);
        builder.Services.AddSingleton(Launcher.Default);
        builder.Services.AddSingleton<PreferencesSettingsService>();
        AddDiagnosticLog(builder);

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        // Every HTTP client keeps the 30-second timeout the app used before it had a client factory.
        builder.Services.ConfigureHttpClientDefaults(http =>
            http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30)));
        builder.Services.AddSingleton(TimeProvider.System);
        // Random.Shared is thread-safe; WheelSpinService picks spin winners from it.
        builder.Services.AddSingleton(Random.Shared);
        builder.Services.AddHttpClient<GitHubReleaseUpdateChecker>();
        builder.Services.AddSingleton<ApplicationUpdateService>();
        var environment = EnvironmentOverrides.Read(Environment.GetEnvironmentVariable);
        builder.Services.AddSingleton(environment);
        builder.Services.AddSingleton(environment.Api);
        builder.Services.AddSingleton(environment.Events);
        builder.Services.AddSingleton<SecureStorageStreamerSongListCredentialStore>();
        builder.Services.AddSingleton<IStreamerSongListCredentialProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        builder.Services.AddSingleton<IStreamerSongListCredentialStore>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        builder.Services.AddSingleton<ApiCredentialTest>();
        builder.Services.AddHttpClient<ISpinnerApiService, StreamerSongListApiClient>();
        builder.Services.AddScoped<NowPlayingTransitionService>();
        builder.Services.AddScoped<StreamerSessionService>();
        builder.Services.AddScoped<WheelSpinService>();
        builder.Services.AddScoped<WinnerActionService>();
        builder.Services.AddSingleton<IStreamerSongListEventSource, CentrifugoStreamerSongListEventSource>();
        builder.Services.AddSingleton<OverlayStateService>();
        builder.Services.AddSingleton<LocalOverlayServer>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    // One provider instance serves both the logging pipeline and the Settings debug-mode toggle, which
    // switches the file on and off. App enables it from the saved settings at startup.
    private static void AddDiagnosticLog(MauiAppBuilder builder)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonglistSpinner",
            "logs");
        var diagnosticLog = new DiagnosticFileLoggerProvider(logDirectory, TimeProvider.System);
        builder.Services.AddSingleton(diagnosticLog);
        builder.Logging.AddProvider(diagnosticLog);
        // The file keeps the app's own debug detail but only warnings and errors from the frameworks.
        builder.Logging.AddFilter<DiagnosticFileLoggerProvider>("SonglistSpinner", LogLevel.Debug);
        builder.Logging.AddFilter<DiagnosticFileLoggerProvider>(null, LogLevel.Warning);
    }
}
