using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SonglistSpinner.Services;

public static class LocalSettingsServiceCollectionExtensions
{
    /// <summary>Registers the settings the user saves on this machine, which live in MAUI preferences.</summary>
    public static IServiceCollection AddLocalSettings(this IServiceCollection services)
    {
        services.TryAddSingleton(Preferences.Default);
        services.TryAddSingleton<IKeyValueStore, MauiPreferencesStore>();
        services.AddSingleton<PreferencesSettingsService>();
        return services;
    }
}
