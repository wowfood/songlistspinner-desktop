using Microsoft.Extensions.Logging;

namespace SonglistSpinner.Services;

public static class LocalOverlayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OBS overlay: its shared state, and the local HTTP server that streams it to browser sources
    /// on <paramref name="port"/> (<see cref="LocalOverlayServer.DefaultPort"/> outside tests).
    /// </summary>
    public static IServiceCollection AddLocalOverlay(this IServiceCollection services, int port)
    {
        services.AddSingleton<OverlayStateService>();
        services.AddSingleton(serviceProvider => new LocalOverlayServer(
            serviceProvider.GetRequiredService<OverlayStateService>(),
            serviceProvider.GetRequiredService<ILogger<LocalOverlayServer>>(),
            port));
        return services;
    }
}
