using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The connection wizard a first run opens: the access token, the channel and its three checks, and the Ready
/// summary. Each test starts its own app with no credential, as a new user's first launch would.
/// </summary>
public class FirstRunSetupTests
{
    private const string Token = "simulator-token";

    [Fact(Timeout = 180_000)]
    public async Task Given_NoSavedCredential_When_TheAppStarts_Then_ItOpensTheSetupWizardAtTheAccessStep()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await StartFirstRunAsync(cancellationToken);

        var setup = scenario.Setup;
        await Expect(setup.Heading).ToBeVisibleAsync();
        await Expect(setup.StepHeading).ToHaveTextAsync("StreamerSongList access");
        await Expect(setup.TokenHint).ToHaveTextAsync("The token is stored in Windows secure storage.");
        await Expect(setup.CurrentStep).ToHaveTextAsync(new Regex("Access$"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheAccessStep_When_ContinuingWithoutAToken_Then_TheWizardAsksForOne()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        var setup = scenario.Setup;

        await setup.EnterTokenAsync("");

        await Expect(setup.Error).ToHaveTextAsync("Paste a StreamerSongList access token to continue.");
        await Expect(setup.StepHeading).ToHaveTextAsync("StreamerSongList access");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ATokenTheApiRejects_When_VerifyingTheChannel_Then_TheWizardReportsTheRejectionFromTheStreamerLookup()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        await new ChannelSeed("setup_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, "wrong-token");

        await setup.ConnectAsync("setup_streamer");

        await Expect(setup.Error).ToHaveTextAsync(
            "StreamerSongList rejected the configured API token. invalid access token");
        await Expect(setup.CheckMessage("API and channel")).ToHaveTextAsync("Connection failed");
        var lookup = Assert.Single(scenario.Simulator.Requests);
        Assert.True(ApiCalls.ResolveStreamer("setup_streamer")(lookup));
        Assert.Equal((401, "Streamer wrong-token"), (lookup.StatusCode, lookup.Authorization));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ATokenTheApiRejects_When_VerifyingTheChannel_Then_TheTokenIsNotKeptAndTheDashboardStillAsksForSetup()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        await new ChannelSeed("setup_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, "wrong-token");

        await setup.ConnectAsync("setup_streamer");
        await Expect(setup.CheckMessage("API and channel")).ToHaveTextAsync("Connection failed");

        // Setup saves the token before testing it; a failed test must take it out of the profile again.
        var secrets = Path.Combine(scenario.Profile.Directory, "secrets.json");
        Assert.DoesNotContain("wrong-token", File.Exists(secrets) ? await File.ReadAllTextAsync(secrets, cancellationToken) : "");
        await scenario.Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true }).ClickAsync();
        await Expect(scenario.Page).ToHaveURLAsync(new Regex("/setup$"));
        await Expect(setup.CurrentStep).ToHaveTextAsync(new Regex("Access$"));
        await Expect(setup.TokenHint).ToHaveTextAsync("The token is stored in Windows secure storage.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AValidTokenAndAChannelWithQueueAndHistory_When_VerifyingIt_Then_TheReadyStepSummarisesTheChannel()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        var channel = await SeedSetupStreamerAsync(scenario);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);

        await setup.ConnectAsync("setup_streamer");

        await Expect(setup.StepHeading).ToHaveTextAsync("SonglistSpinner is ready");
        await Expect(setup.CurrentStep).ToHaveTextAsync(new Regex("Ready$"));
        await Expect(setup.ResolvedChannel).ToHaveTextAsync(
            $"setup_streamer resolved to StreamerSongList channel #{channel.StreamerId}.");
        await Expect(setup.Identities).ToHaveTextAsync(["Twitch setup_streamer"]);
        // Three upcoming requests (Now Playing is not queued) and both plays inside the default week of history.
        await Expect(setup.SummaryValue("Queued songs")).ToHaveTextAsync("3");
        await Expect(setup.SummaryValue("History items")).ToHaveTextAsync("2");
        await Expect(setup.SummaryValue("Realtime")).ToHaveTextAsync("✓");
        await Expect(setup.SummaryValue("Overlay")).ToHaveTextAsync("✓");
        await Expect(setup.OverlayUrl).ToHaveTextAsync(scenario.App.OverlayUri.ToString());
        await Expect(setup.Warning).ToHaveCountAsync(0);
        Assert.Equal(
            "Streamer simulator-token",
            (await scenario.Simulator.WaitForFirstRequestAsync(ApiCalls.ResolveStreamer("setup_streamer"), cancellationToken))
            .Authorization);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheReadyStep_When_OpeningTheDashboard_Then_TheDashboardLoadsTheChannelSetupSaved()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        await SeedSetupStreamerAsync(scenario);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);
        await setup.ConnectAsync("setup_streamer");
        await Expect(setup.StepHeading).ToHaveTextAsync("SonglistSpinner is ready");

        await setup.OpenDashboardButton.ClickAsync();

        var dashboard = scenario.Dashboard;
        await Expect(dashboard.StreamerLabel).ToHaveTextAsync("Streamer: setup_streamer");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 3 songs. Press SPIN!");
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("setup_streamer");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheRealtimeServiceRefusesConnections_When_VerifyingTheChannel_Then_TheChannelStepReportsWhichChecksPassed()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        var channel = await SeedSetupStreamerAsync(scenario);
        scenario.Simulator.RejectEventConnections = true;
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);

        await setup.ConnectAsync("setup_streamer");

        // The realtime check gives up after ten seconds; the API and overlay checks around it pass.
        await Expect(setup.Error).ToHaveTextAsync(
            "The channel is connected, but one supporting service could not be verified.");
        await Expect(setup.CheckMessage("API and channel")).ToHaveTextAsync(
            $"Connected to channel #{channel.StreamerId} with 3 queued song(s).");
        await Expect(setup.Check("Realtime events")).ToHaveClassAsync(new Regex(@"\bfailed\b"));
        await Expect(setup.CheckMessage("Realtime events")).Not.ToHaveTextAsync(
            "Realtime queue and history updates are available.");
        await Expect(setup.CheckMessage("Local OBS overlay")).ToHaveTextAsync(
            $"Overlay available at {scenario.App.OverlayUri}.");
        await Expect(setup.ContinueWithWarningsButton).ToBeVisibleAsync();
        await Expect(setup.StepHeading).ToHaveTextAsync("Choose your channel");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheRealtimeCheckFailed_When_ContinuingWithWarnings_Then_TheReadyStepFlagsRealtimeForAttention()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        await SeedSetupStreamerAsync(scenario);
        scenario.Simulator.RejectEventConnections = true;
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);
        await setup.ConnectAsync("setup_streamer");

        await setup.ContinueWithWarningsButton.ClickAsync();

        await Expect(setup.StepHeading).ToHaveTextAsync("SonglistSpinner is ready");
        await Expect(setup.Warning).ToHaveTextAsync(
            "The API connection is ready, but one optional check needs attention. You can review it later in Settings.");
        await Expect(setup.SummaryValue("Realtime")).ToHaveTextAsync("!");
        await Expect(setup.SummaryValue("Overlay")).ToHaveTextAsync("✓");
        await Expect(setup.SummaryValue("Queued songs")).ToHaveTextAsync("3");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AKickChannelAddress_When_VerifyingIt_Then_TheChannelIsLookedUpOnKick()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        var channel = await new ChannelSeed("kick_streamer", "kick")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);

        await setup.ConnectAsync("https://streamersonglist.com/k/kick_streamer");

        await Expect(setup.StepHeading).ToHaveTextAsync("SonglistSpinner is ready");
        await Expect(setup.ResolvedChannel).ToHaveTextAsync(
            $"kick_streamer resolved to StreamerSongList channel #{channel.StreamerId}.");
        await Expect(setup.Identities).ToHaveTextAsync(["Kick kick_streamer"]);
        await Expect(setup.SummaryValue("Queued songs")).ToHaveTextAsync("2");
        var lookup = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.ResolveStreamer("kick_streamer", "kick"), cancellationToken);
        Assert.Equal(200, lookup.StatusCode);
        Assert.All(
            scenario.Simulator.Requests,
            request => Assert.Equal("kick", request.Query.GetValueOrDefault("platform")));
    }

    [Theory(Timeout = 180_000)]
    [InlineData("ftp://example.com/t/x", "The streamer URL must use http or https.")]
    [InlineData("https://example.com/q/name", "Use a streamer name or a URL ending in /t/name, /s/name, /k/name, or /y/name.")]
    public async Task Given_AnUnusableChannelAddress_When_VerifyingIt_Then_TheWizardExplainsWithoutCallingTheApi(
        string reference,
        string expectedError)
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);

        await setup.ConnectAsync(reference);

        // The address is checked before any request starts, so the request log is final once the error shows.
        await Expect(setup.Error).ToHaveTextAsync(expectedError);
        await Expect(setup.CheckMessage("API and channel")).ToHaveTextAsync("Not checked yet");
        Assert.Empty(scenario.Simulator.Requests);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AChannelStreamerSongListDoesNotKnow_When_VerifyingIt_Then_TheWizardReportsItNotFound()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await StartFirstRunAsync(cancellationToken);
        await new ChannelSeed("setup_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var setup = scenario.Setup;
        await ReachChannelStepAsync(setup, Token);

        await setup.ConnectAsync("nobody");

        await Expect(setup.Error).ToHaveTextAsync("StreamerSongList returned HTTP 404 (Not Found). streamer not found");
        await Expect(setup.CheckMessage("API and channel")).ToHaveTextAsync("Connection failed");
        var lookup = Assert.Single(scenario.Simulator.Requests);
        Assert.True(ApiCalls.ResolveStreamer("nobody")(lookup));
        Assert.Equal(404, lookup.StatusCode);
    }

    private static Task<AppScenario> StartFirstRunAsync(CancellationToken cancellationToken) =>
        AppScenario.StartAsync(cancellationToken, useEnvironmentCredential: false);

    private static Task<SeededChannel> SeedSetupStreamerAsync(AppScenario scenario) =>
        new ChannelSeed("setup_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside, SongCatalog.Africa)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.DontStopMeNow, TimeSpan.FromDays(3))
            .ApplyAsync(scenario.Simulator);

    private static async Task ReachChannelStepAsync(SetupWizard setup, string token)
    {
        await Expect(setup.StepHeading).ToHaveTextAsync("StreamerSongList access");
        await setup.EnterTokenAsync(token);
        await Expect(setup.StepHeading).ToHaveTextAsync("Choose your channel");
    }
}
