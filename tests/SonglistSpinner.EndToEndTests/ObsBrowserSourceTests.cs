using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class ObsBrowserSourceTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannel_When_AnObsBrowserSourceOpensTheOverlay_Then_ItRendersTheChannelWheelAndPlayedSongs()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("obs_streamer")
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(2))
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("obs_streamer");

        var overlay = await scenario.OpenOverlayAsync();

        await Expect(overlay.StreamerLabel).ToHaveTextAsync("obs_streamer");
        await overlay.ExpectWheelLabelsAsync("a-ha - Take On Me (synth_lover)", "Toto - Africa (long_time_fan)");
        await Expect(overlay.QueuedCount).ToHaveTextAsync("2");
        await Expect(overlay.PlayedCount).ToHaveTextAsync("1");
        await overlay.PlayedList.ExpectLinesAsync("Artist: Daft Punk | Title: Get Lucky");
        // The Dashboard counts the browser source as the one connected overlay.
        await Expect(dashboard.Health.Overlay).ToHaveTextAsync("1 connected");
    }
}
