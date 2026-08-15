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

        builder.Services.AddSingleton<ILocalSettingsService, PreferencesSettingsService>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<GitHubReleaseUpdateChecker>();
        builder.Services.AddSingleton<ApplicationUpdateService>();
        builder.Services.AddSingleton(CreateStreamerSongListApiOptions());
        builder.Services.AddSingleton(CreateStreamerSongListEventsOptions());
        builder.Services.AddSingleton<SecureStorageStreamerSongListCredentialStore>();
        builder.Services.AddSingleton<IStreamerSongListCredentialProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        builder.Services.AddSingleton<IStreamerSongListCredentialStore>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        builder.Services.AddScoped<ISpinnerApiService, StreamerSongListApiClient>();
        builder.Services.AddScoped<NowPlayingTransitionService>();
        builder.Services.AddSingleton<IStreamerSongListEventSource, CentrifugoStreamerSongListEventSource>();
        builder.Services.AddSingleton<OverlayStateService>();
        builder.Services.AddSingleton<LocalOverlayServer>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static StreamerSongListApiOptions CreateStreamerSongListApiOptions()
    {
        var configuredAddress = Environment.GetEnvironmentVariable("SONGLISTSPINNER_SSL_API_BASE_URL");
        var baseAddress = Uri.TryCreate(configuredAddress, UriKind.Absolute, out var parsedAddress)
            ? parsedAddress
            : StreamerSongListApiOptions.StagingBaseAddress;

        return new StreamerSongListApiOptions { BaseAddress = baseAddress };
    }

    private static StreamerSongListEventsOptions CreateStreamerSongListEventsOptions()
    {
        var configuredEndpoint = Environment.GetEnvironmentVariable("SONGLISTSPINNER_SSL_EVENTS_URL");
        var endpoint = Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var parsedEndpoint)
            ? parsedEndpoint
            : StreamerSongListEventsOptions.StagingEndpoint;

        return new StreamerSongListEventsOptions { Endpoint = endpoint };
    }
}
