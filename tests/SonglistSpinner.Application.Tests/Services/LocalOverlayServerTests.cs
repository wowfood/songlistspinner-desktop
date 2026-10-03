using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

/// <remarks>The server's HTTP behaviour is tested in SonglistSpinner.IntegrationTests.</remarks>
public class LocalOverlayServerTests
{
    [Fact]
    public async Task Given_TheAppsRegistration_When_ResolvingTheServer_Then_OverlaysUseTheDefaultPort()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<LocalOverlayServer>>(NullLogger<LocalOverlayServer>.Instance);
        // As Desktop's AddLocalOverlay registers it: the port comes from the constructor's default.
        services.AddSingleton<OverlayStateService>();
        services.AddSingleton<LocalOverlayServer>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        var server = provider.GetRequiredService<LocalOverlayServer>();

        Assert.Equal(LocalOverlayServer.DefaultPort, server.Port);
        Assert.Equal("http://localhost:5150/overlay", server.OverlayUrl);
    }
}
