using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The Dashboard's Now Playing panel text, as set in Settings &gt; Overlay Layout &gt; Now Playing Panel (whose
/// fields show only while the Now Playing workflow is on).
/// </summary>
public class NowPlayingDisplayTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string Channel = "now_playing_streamer";

    [Fact(Timeout = 180_000)]
    public async Task Given_ASongIsPlayingAndDefaultSettings_When_TheChannelLoads_Then_TheNowPlayingPanelShowsItsLabelledArtistAndTitle()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedDreamsPlaying().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        // The Now Playing workflow is off by default; the Dashboard still shows what StreamerSongList is playing.
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASongIsPlaying_When_TheWorkflowIsOnAndNowPlayingLabelsAreTurnedOffInSettings_Then_TheNowPlayingPanelShowsOnlyTheValues()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedDreamsPlaying().ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.NowPlayingShowLabels.UncheckAsync();
        await settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();

        await dashboard.LoadChannelAsync(Channel);

        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Fleetwood Mac | Dreams");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NowPlayingFieldsWithTheRequesterAndTheDashSeparator_When_TheChannelLoads_Then_TheNowPlayingPanelJoinsThreeFieldsWithDashes()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(new
            {
                DisplayNowPlaying = true,
                NowPlayingFields = """["artist","title","requester"]""",
                NowPlayingSeparator = " — "
            });
            return Task.CompletedTask;
        });
        await SeedDreamsPlaying().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync(
            "Artist: Fleetwood Mac — Title: Dreams — Requester: night_owl");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASongIsPlaying_When_TheRequesterAndACustomSeparatorAreChosenInSettings_Then_TheNowPlayingPanelJoinsThreeFieldsWithIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedDreamsPlaying().ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        var layout = settings.OverlayLayout;
        await layout.OpenAsync();
        await layout.NowPlayingFields.SetSelectedAsync("Requester", true);
        await layout.NowPlayingSeparator.SelectOptionAsync("custom");
        await layout.NowPlayingCustomSeparator.FillAsync(" ~ ");
        // The field saves its value on change, which a text input raises when it loses focus.
        await layout.NowPlayingCustomSeparator.BlurAsync();
        await settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();

        await dashboard.LoadChannelAsync(Channel);

        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac ~ Title: Dreams ~ Requester: night_owl");
    }

    private static ChannelSeed SeedDreamsPlaying() =>
        new ChannelSeed(Channel)
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe);
}
