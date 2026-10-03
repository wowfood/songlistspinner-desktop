using Microsoft.Extensions.DependencyInjection.Extensions;
using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;

namespace SonglistSpinner.Services;

public static class StreamerSongListServiceCollectionExtensions
{
    /// <summary>
    /// Registers the StreamerSongList API client, the realtime event source and the API credential, which is
    /// kept in Windows secure storage with the environment's credential as a fallback.
    /// </summary>
    public static IServiceCollection AddStreamerSongList(
        this IServiceCollection services,
        EnvironmentOverrides environment)
    {
        services.AddSingleton(environment);
        services.AddSingleton(environment.Api);
        services.AddSingleton(environment.Events);

        services.TryAddSingleton(SecureStorage.Default);
        services.TryAddSingleton(Preferences.Default);
        services.AddSingleton<SecureStorageStreamerSongListCredentialStore>();
        services.AddSingleton<IStreamerSongListCredentialProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        services.AddSingleton<IStreamerSongListCredentialStore>(serviceProvider =>
            serviceProvider.GetRequiredService<SecureStorageStreamerSongListCredentialStore>());
        services.AddSingleton<ApiCredentialTest>();

        services.AddHttpClient<ISpinnerApiService, StreamerSongListApiClient>();
        services.AddSingleton<IStreamerSongListEventSource, CentrifugoStreamerSongListEventSource>();
        return services;
    }
}
