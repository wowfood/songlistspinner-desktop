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
        // This (Desktop) assembly carries the release version; ApplicationUpdateService's own assembly does not.
        var currentVersion = typeof(ApplicationUpdateServiceCollectionExtensions).Assembly.GetName().Version ??
                             new Version(0, 0, 0);
        services.AddSingleton(provider => new ApplicationUpdateService(
            provider.GetRequiredService<GitHubReleaseUpdateChecker>(),
            provider.GetRequiredService<IKeyValueStore>(),
            currentVersion));
        return services;
    }
}
