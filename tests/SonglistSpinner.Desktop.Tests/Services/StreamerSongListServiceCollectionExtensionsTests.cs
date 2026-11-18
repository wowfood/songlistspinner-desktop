using Microsoft.Extensions.DependencyInjection;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Desktop.Tests.Services;

/// <remarks>
/// The app registers its features in this order (MauiProgram): the test profile when there is one, then local
/// settings, then StreamerSongList. A test profile must keep every saved value, the API token above all, out of
/// the developer's own preferences and Windows secure storage.
/// </remarks>
public class StreamerSongListServiceCollectionExtensionsTests
{
    private static readonly EnvironmentOverrides NoOverrides =
        EnvironmentOverrides.Read(_ => null, allowDevelopmentOverrides: false);

    [Fact]
    public void Given_NoTestProfile_When_TheAppsServicesAreRegistered_Then_TheTokenIsKeptInSecureStorage()
    {
        var services = new ServiceCollection();

        services.AddLocalSettings().AddStreamerSongList(NoOverrides);

        var secrets = Assert.Single(services, service => service.ServiceType == typeof(ISecretStore));
        Assert.Equal(typeof(MauiSecureStorageStore), secrets.ImplementationType);
        var savedValues = Assert.Single(services, service => service.ServiceType == typeof(IKeyValueStore));
        Assert.Equal(typeof(MauiPreferencesStore), savedValues.ImplementationType);
    }

    [Fact]
    public void Given_ATestProfileRegisteredFirst_When_TheAppsServicesAreRegistered_Then_TheTokenAndSavedValuesStayInTheProfilesFiles()
    {
        var profileDirectory = Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();

        services.AddIsolatedProfile(profileDirectory).AddLocalSettings().AddStreamerSongList(NoOverrides);

        // What the app would be given, which is the last registration of each service.
        using var provider = services.BuildServiceProvider();
        Assert.IsType<PlaintextFileSecretStore>(provider.GetRequiredService<ISecretStore>());
        Assert.IsType<JsonFileKeyValueStore>(provider.GetRequiredService<IKeyValueStore>());
        Assert.DoesNotContain(services, service => service.ImplementationType == typeof(MauiSecureStorageStore));
        Assert.DoesNotContain(services, service => service.ImplementationType == typeof(MauiPreferencesStore));
    }
}
