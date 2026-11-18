using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Settings &gt; Spinner &amp; Queue &gt; "Exclude already-played songs from the wheel": which queued requests the
/// wheel offers when some were already played within the play history period.
/// </summary>
public class PlayedSongExclusionTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string Channel = "exclusion_streamer";

    [Fact(Timeout = 180_000)]
    public async Task Given_AQueuedSongPlayedAnHourAgoAndExclusionOff_When_TheChannelLoads_Then_TheWheelOffersEveryQueuedSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedGetLuckyPlayedAndQueuedAgain(TimeSpan.FromHours(1)).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.ExpectWheelLabelsAsync(SongCatalog.GetLucky.WheelLabel, SongCatalog.TakeOnMe.WheelLabel);
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("2");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 2 songs. Press SPIN!");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AQueuedSongPlayedAnHourAgo_When_ExclusionIsTurnedOnInSettings_Then_TheWheelOffersOnlyTheUnplayedSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedGetLuckyPlayedAndQueuedAgain(TimeSpan.FromHours(1)).ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.ExcludePlayedSongs.CheckAsync();
        await settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.ExpectWheelLabelsAsync(SongCatalog.TakeOnMe.WheelLabel);
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("1");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 1 songs. Press SPIN!");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ExclusionOverTheLast24HoursAndASongPlayedThreeDaysAgo_When_TheChannelLoads_Then_TheWheelStillOffersThatSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(
            new { ExcludePlayedSongs = true, PlayHistoryPeriod = "day" },
            cancellationToken);
        await SeedGetLuckyPlayedAndQueuedAgain(TimeSpan.FromDays(3)).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.ExpectWheelLabelsAsync(SongCatalog.GetLucky.WheelLabel, SongCatalog.TakeOnMe.WheelLabel);
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("2");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ExclusionOnAndEveryQueuedSongAlreadyPlayed_When_SpinIsPressed_Then_TheDashboardSaysNoSongsAreLeft()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { ExcludePlayedSongs = true }, cancellationToken);
        await new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.GetLucky)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync(Channel);
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 0 songs. Press SPIN!");

        await dashboard.SpinButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("No songs left to spin!");
        await Expect(dashboard.QueueEmptyState).ToHaveTextAsync("No eligible queued songs are available to spin.");
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("0");
    }

    // Get Lucky was played, then requested again; Take On Me was never played.
    private static ChannelSeed SeedGetLuckyPlayedAndQueuedAgain(TimeSpan playedAgo) =>
        new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.GetLucky, playedAgo)
            .WithQueued(SongCatalog.GetLucky, SongCatalog.TakeOnMe);

    private static Task<AppScenario> StartWithSettingsAsync(object settings, CancellationToken cancellationToken) =>
        AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(settings);
            return Task.CompletedTask;
        });
}
