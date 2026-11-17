using Microsoft.Extensions.DependencyInjection;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Desktop.Tests.Services;

/// <remarks>
/// The registrations are read without building the container: the MAUI preferences they register exist only inside
/// a running app.
/// </remarks>
public class LocalSettingsServiceCollectionExtensionsTests
{
    [Fact]
    public void Given_NoTestProfile_When_LocalSettingsAreRegistered_Then_SavedValuesAreKeptInMauiPreferences()
    {
        var services = new ServiceCollection();

        services.AddLocalSettings();

        var store = Assert.Single(services, service => service.ServiceType == typeof(IKeyValueStore));
        Assert.Equal(typeof(MauiPreferencesStore), store.ImplementationType);
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(ISecretStore));
    }

    [Fact]
    public void Given_ATestProfileRegisteredFirst_When_LocalSettingsAreRegistered_Then_TheProfilesFilesAreKept()
    {
        var profileDirectory = Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();

        services.AddIsolatedProfile(profileDirectory).AddLocalSettings();

        var store = Assert.Single(services, service => service.ServiceType == typeof(IKeyValueStore));
        Assert.IsType<JsonFileKeyValueStore>(store.ImplementationInstance);
        var secrets = Assert.Single(services, service => service.ServiceType == typeof(ISecretStore));
        Assert.IsType<PlaintextFileSecretStore>(secrets.ImplementationInstance);
    }
}
