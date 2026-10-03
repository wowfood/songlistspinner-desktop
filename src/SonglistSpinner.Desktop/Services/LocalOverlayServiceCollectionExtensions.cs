namespace SonglistSpinner.Services;

public static class LocalOverlayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OBS overlay: its shared state, and the local HTTP server that streams it to browser sources.
    /// </summary>
    public static IServiceCollection AddLocalOverlay(this IServiceCollection services)
    {
        services.AddSingleton<OverlayStateService>();
        services.AddSingleton<LocalOverlayServer>();
        return services;
    }
}
