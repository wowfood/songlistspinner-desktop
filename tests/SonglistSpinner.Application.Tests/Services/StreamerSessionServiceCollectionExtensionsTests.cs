using Microsoft.Extensions.DependencyInjection;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class StreamerSessionServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Given_TheServicesTheHostRegisters_When_AddingTheStreamerSession_Then_TheDashboardsServicesResolve()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamerSongListClient>(new ScriptedStreamerSongListClient());
        services.AddSingleton<IStreamerSongListEventSource>(new ChannelEventSource());
        services.AddSingleton<OverlayStateService>();
        services.AddSingleton(TimeProvider.System);

        services.AddStreamerSession();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        var session = scope.ServiceProvider.GetRequiredService<StreamerSessionService>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ChannelLoader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WheelSpinService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WinnerActionService>());
        // Pages resolved later in the same window share the one session.
        Assert.Same(session, scope.ServiceProvider.GetRequiredService<StreamerSessionService>());
    }
}
