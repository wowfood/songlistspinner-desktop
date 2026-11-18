namespace SonglistSpinner.Services;

public static class IsolatedProfileServiceCollectionExtensions
{
    public const string PreferencesFileName = "preferences.json";
    public const string SecretsFileName = "secrets.json";

    /// <summary>
    /// TEST ONLY: keeps the saved settings and the API credential in files under <paramref name="profileDirectory"/>
    /// instead of MAUI preferences and Windows secure storage. The credential file is plain text. It must be added
    /// before the features that register the MAUI stores, which only add theirs when none is registered.
    /// </summary>
    public static IServiceCollection AddIsolatedProfile(this IServiceCollection services, string profileDirectory)
    {
        services.AddSingleton<IKeyValueStore>(
            new JsonFileKeyValueStore(Path.Combine(profileDirectory, PreferencesFileName)));
        services.AddSingleton<ISecretStore>(
            new PlaintextFileSecretStore(Path.Combine(profileDirectory, SecretsFileName)));
        return services;
    }
}
