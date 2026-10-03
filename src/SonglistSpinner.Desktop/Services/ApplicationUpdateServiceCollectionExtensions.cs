using Microsoft.Extensions.DependencyInjection.Extensions;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

public static class ApplicationUpdateServiceCollectionExtensions
{
    /// <summary>
    /// Registers the check for a newer GitHub release, and the record of which release the user dismissed.
    /// </summary>
    public static IServiceCollection AddApplicationUpdates(this IServiceCollection services)
    {
        services.TryAddSingleton(Preferences.Default);
        services.AddHttpClient<GitHubReleaseUpdateChecker>();
        services.AddSingleton<ApplicationUpdateService>();
        return services;
    }
}
