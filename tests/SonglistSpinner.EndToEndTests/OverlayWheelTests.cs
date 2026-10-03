using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>The overlay's wheel and channel header as an OBS browser source shows them.</summary>
public class OverlayWheelTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_AnOverlayOpenedBeforeAnyChannel_When_TheDashboardLoadsOne_Then_TheOverlayWheelShowsItsQueue()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("overlay_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        // The Dashboard's connected count is no proof this page connected: the overlay server notices a closed
        // browser source (an earlier test's) only when a later heartbeat write to it fails, which can be 20 s on.
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("Waiting for Dashboard...");
        await Expect(overlay.PlayedCount).ToHaveTextAsync("0");
        await Expect(overlay.QueuedCount).ToHaveTextAsync("0");
        await overlay.ExpectWheelLabelsAsync("Waiting for Dashboard...");

        await dashboard.LoadChannelAsync("overlay_streamer");

        await Expect(overlay.StreamerLabel).ToHaveTextAsync("overlay_streamer");
        await overlay.ExpectWheelLabelsAsync("a-ha - Take On Me (synth_lover)", "Toto - Africa (long_time_fan)");
        await Expect(overlay.QueuedCount).ToHaveTextAsync("2");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannelInTheOverlay_When_TheDashboardChangesChannel_Then_TheOverlayWaitsForTheDashboardAgain()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("overlay_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("overlay_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("overlay_streamer");
        await overlay.ExpectWheelLabelsAsync("a-ha - Take On Me (synth_lover)", "Toto - Africa (long_time_fan)");

        await dashboard.ChangeChannelAsync();

        await Expect(overlay.StreamerLabel).ToHaveTextAsync("Waiting for Dashboard...");
        await overlay.ExpectWheelLabelsAsync("Waiting for Dashboard...");
        await Expect(overlay.QueuedCount).ToHaveTextAsync("0");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AnOpenOverlay_When_ShowWheelIsCleared_Then_TheOverlayHidesItsWheel()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("overlay_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("overlay_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("overlay_streamer");
        await Expect(overlay.WheelContents).ToBeVisibleAsync();

        await dashboard.SetWheelVisibleAsync(false);

        await Expect(overlay.WheelContents).ToBeHiddenAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AHiddenOverlayWheel_When_ShowWheelIsCheckedAgain_Then_TheOverlayShowsItsWheel()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("overlay_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("overlay_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("overlay_streamer");
        await dashboard.SetWheelVisibleAsync(false);
        await Expect(overlay.WheelContents).ToBeHiddenAsync();

        await dashboard.SetWheelVisibleAsync(true);

        await Expect(overlay.WheelContents).ToBeVisibleAsync();
        await overlay.ExpectWheelLabelsAsync("a-ha - Take On Me (synth_lover)");
    }
}
