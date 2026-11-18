using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Settings &gt; Overlay Layout &gt; Played Songs Panel: how each played song reads on the Dashboard (its fields and
/// their order, labels, separator, sequence numbers and the header row).
/// </summary>
public class PlayedListFormattingTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string Channel = "format_streamer";

    [Fact(Timeout = 180_000)]
    public async Task Given_TwoPlays_When_SequenceNumbersAreTurnedOnInSettings_Then_TheOldestPlayIsNumberedOneAtTheBottom()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedTwoPlays().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        await layout.PlayedShowNumbers.CheckAsync();
        // "Bottom of list" is the default numbering start.
        await Expect(layout.PlayedNumberingStart).ToHaveValueAsync("bottom");
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(
            "2. Artist: Daft Punk | Title: Get Lucky",
            "1. Artist: Queen | Title: Don't Stop Me Now");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_SequenceNumbersStartingAtTheTop_When_TheChannelLoads_Then_TheNewestPlayIsNumberedOne()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(
            new { PlayedListShowNumbers = true, PlayedListNumberingStart = "top" },
            cancellationToken);
        await SeedTwoPlays().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(
            "1. Artist: Daft Punk | Title: Get Lucky",
            "2. Artist: Queen | Title: Don't Stop Me Now");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TwoPlays_When_FieldLabelsAreTurnedOffInSettings_Then_EachLineShowsOnlyTheValues()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedTwoPlays().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        await layout.PlayedShowLabels.UncheckAsync();
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync("Daft Punk | Get Lucky", "Queen | Don't Stop Me Now");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_APlay_When_TheBulletSeparatorIsChosenInSettings_Then_TheFieldsAreJoinedByABullet()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedOnePlay().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        await layout.PlayedSeparator.SelectOptionAsync("bullet");
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync("Artist: Daft Punk • Title: Get Lucky");
        Assert.Equal(["Artist: Daft Punk • Title: Get Lucky"], await dashboard.PlayedList.ReadLinesAsync());
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDashSeparator_When_TheChannelLoads_Then_TheFieldsAreJoinedByADash()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(new { PlayedListSeparator = " — " }, cancellationToken);
        await SeedOnePlay().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync("Artist: Daft Punk — Title: Get Lucky");
        Assert.Equal(["Artist: Daft Punk — Title: Get Lucky"], await dashboard.PlayedList.ReadLinesAsync());
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_APlay_When_ACustomSeparatorIsEnteredInSettings_Then_TheFieldsAreJoinedByItWithItsSpacesKept()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedOnePlay().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        await layout.PlayedSeparator.SelectOptionAsync("custom");
        await layout.PlayedCustomSeparator.FillAsync(" ~ ");
        // The field saves its value on change, which a text input raises when it loses focus.
        await layout.PlayedCustomSeparator.BlurAsync();
        await Expect(layout.PlayedCustomSeparator).ToHaveValueAsync(" ~ ");
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync("Artist: Daft Punk ~ Title: Get Lucky");
        Assert.Equal("Artist: Daft Punk ~ Title: Get Lucky", (await dashboard.PlayedList.ReadLinesAsync())[0]);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TwoPlays_When_TheHeaderRowAndNumbersAreTurnedOnInSettings_Then_TheListIsATableUnderFieldHeaders()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedTwoPlays().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        await layout.PlayedShowHeaders.CheckAsync();
        await layout.PlayedShowNumbers.CheckAsync();
        // The header row names the fields, so per-value labels cannot be turned on alongside it.
        await Expect(layout.PlayedShowLabels).ToBeDisabledAsync();
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        var playedList = dashboard.PlayedList;
        await playedList.ExpectHeadersAsync("#", "Artist", "Title");
        await playedList.ExpectTableRowsAsync(
            ["2.", "Daft Punk", "Get Lucky"],
            ["1.", "Queen", "Don't Stop Me Now"]);
        await playedList.ExpectTableSeparatorAsync(" | ");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_FieldsReorderedAndExtendedInSettings_When_TheChannelLoads_Then_EachLineFollowsTheChosenFieldsAndOmitsAMissingDonation()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedPlaysWithAndWithoutATip().ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.SelectPlayHistoryPeriodAsync("month");
        await settings.OverlayLayout.OpenAsync();
        var fields = settings.OverlayLayout.PlayedFields;
        await fields.MoveEarlierAsync("Song title");
        await fields.SetSelectedAsync("Requester", true);
        await fields.SetSelectedAsync("Donation", true);
        await fields.ExpectOrderAsync("title", "artist", "requester", "donation");
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync(
            "Title: Get Lucky | Artist: Daft Punk | Requester: early_bird",
            "Title: Dancing Queen | Artist: ABBA | Requester: disco_fan | Donation: 5.00");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ArtistMovedLaterInSettings_When_TheChannelLoads_Then_EachLineShowsTheTitleBeforeTheArtist()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SeedOnePlay().ApplyAsync(scenario.Simulator);
        var layout = await OpenPlayedPanelSettingsAsync(scenario.Settings);
        var fields = layout.PlayedFields;
        await fields.ExpectOrderAsync("artist", "title", "requester", "donation");
        await fields.MoveLaterAsync("Artist");
        await fields.ExpectOrderAsync("title", "artist", "requester", "donation");
        var dashboard = await SaveAndOpenDashboardAsync(scenario);

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectLinesAsync("Title: Get Lucky | Artist: Daft Punk");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ExtendedFieldsWithTheHeaderRow_When_TheChannelLoads_Then_APlayWithoutATipHasAnEmptyDonationCell()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartWithSettingsAsync(
            new
            {
                // SettingsDto.PlayedListFields is saved under its older wire name.
                SongListFields = """["title","artist","requester","donation"]""",
                PlayedListShowFieldHeaders = true,
                PlayHistoryPeriod = "month"
            },
            cancellationToken);
        await SeedPlaysWithAndWithoutATip().ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;

        await dashboard.LoadChannelAsync(Channel);

        await dashboard.PlayedList.ExpectHeadersAsync("Title", "Artist", "Requester", "Donation");
        await dashboard.PlayedList.ExpectTableRowsAsync(
            ["Get Lucky", "Daft Punk", "early_bird", ""],
            ["Dancing Queen", "ABBA", "disco_fan", "5.00"]);
    }

    private static ChannelSeed SeedOnePlay() =>
        new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.TakeOnMe);

    // Both within the default period, "Last 7 days".
    private static ChannelSeed SeedTwoPlays() =>
        new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .WithQueued(SongCatalog.TakeOnMe);

    // Get Lucky was requested without a tip and Dancing Queen with 5.00; both fall within "Last month".
    private static ChannelSeed SeedPlaysWithAndWithoutATip() =>
        new ChannelSeed(Channel)
            .WithPlayed(SongCatalog.DancingQueen, TimeSpan.FromDays(20))
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.TakeOnMe);

    private static async Task<OverlayLayoutSettings> OpenPlayedPanelSettingsAsync(SettingsPage settings)
    {
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        return settings.OverlayLayout;
    }

    private static async Task<DashboardPage> SaveAndOpenDashboardAsync(AppScenario scenario)
    {
        await scenario.Settings.SaveAsync();
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        return dashboard;
    }

    private static Task<AppScenario> StartWithSettingsAsync(object settings, CancellationToken cancellationToken) =>
        AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(settings);
            return Task.CompletedTask;
        });
}
