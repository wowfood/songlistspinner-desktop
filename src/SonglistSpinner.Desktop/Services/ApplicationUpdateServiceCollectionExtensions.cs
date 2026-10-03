using Microsoft.Extensions.DependencyInjection.Extensions;
using SonglistSpinner.Core.Updates;

namespace SonglistSpinner.Services;

public static class ApplicationUpdateServiceCollectionExtensions
{
    /// <summary>
    /// Registers the check for a newer release at <paramref name="latestReleaseEndpoint"/> (GitHub outside tests),
    /// and the record of which release the user dismissed.
    /// </summary>
    public static IServiceCollection AddApplicationUpdates(this IServiceCollection services, Uri latestReleaseEndpoint)
    {
        services.TryAddSingleton(Preferences.Default);
        services.TryAddSingleton<IKeyValueStore, MauiPreferencesStore>();
        services.AddHttpClient(nameof(GitHubReleaseUpdateChecker))
            .AddTypedClient(httpClient => new GitHubReleaseUpdateChecker(httpClient, latestReleaseEndpoint));
        services.AddSingleton<ApplicationUpdateService>();
        return services;
    }
}
