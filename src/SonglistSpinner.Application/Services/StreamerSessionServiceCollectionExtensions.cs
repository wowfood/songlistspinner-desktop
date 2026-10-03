using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SonglistSpinner.Core.Winner;

namespace SonglistSpinner.Services;

public static class StreamerSessionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the loaded-channel session and the spin and winner actions that work on it. They are scoped
    /// to the Blazor WebView, which lives as long as the app window, so the session outlives page changes.
    /// The host registers the StreamerSongList API and event source, the overlay state and a TimeProvider.
    /// </summary>
    public static IServiceCollection AddStreamerSession(this IServiceCollection services)
    {
        // Random.Shared is thread-safe; WheelSpinService picks spin winners from it.
        services.TryAddSingleton(Random.Shared);
        services.AddScoped<NowPlayingTransitionService>();
        services.AddScoped<StreamerSessionService>();
        services.AddScoped<WheelSpinService>();
        services.AddScoped<WinnerActionService>();
        return services;
    }
}
