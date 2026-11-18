using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class ChannelSetupTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheEnvironmentToken_When_TheSetupWizardConnectsAChannel_Then_TheWheelHoldsItsQueue()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("setup_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        // The token comes from the environment, so the wizard only needs the channel.
        await scenario.Settings.OpenAsync();
        var setup = scenario.Setup;
        await setup.OpenFromSettingsAsync();
        await Expect(setup.TokenHint).ToHaveTextAsync("A credential is already configured. Leave this blank to keep using it.");
        await setup.EnterTokenAsync("");

        await setup.ConnectAsync("setup_streamer");
        await Expect(setup.StepHeading).ToHaveTextAsync("SonglistSpinner is ready");
        await setup.OpenDashboardButton.ClickAsync();

        // The Dashboard loads the channel Setup saved, and its wheel (which the overlay mirrors) holds the
        // upcoming queue in order, without the Now Playing song.
        var dashboard = scenario.Dashboard;
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: setup_streamer");
        await dashboard.ExpectWheelLabelsAsync(
            "a-ha - Take On Me (synth_lover)",
            "The Killers - Mr. Brightside (indie_kid)",
            "Toto - Africa (long_time_fan)");
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        var initialState = await overlay.NextAsync("init_state", cancellationToken);
        Assert.Equal(
            channel.Queue.Select(entry => entry.QueueId),
            initialState.GetProperty("wheelItems").EnumerateArray().Select(item => item.GetProperty("queueId").GetInt32()));
    }
}
