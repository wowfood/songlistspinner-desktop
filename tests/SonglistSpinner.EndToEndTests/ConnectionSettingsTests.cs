using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Settings &gt; Connection: testing the connection against the default channel with the configured or a new API
/// token, and saving and clearing the stored credential.
/// </summary>
public class ConnectionSettingsTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASeededDefaultChannel_When_TheConnectionIsTested_Then_TheResultCountsItsQueueAndHistory()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("settings_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside, SongCatalog.Africa)
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithPlayed(SongCatalog.Dreams, TimeSpan.FromDays(2))
            .ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        var connection = settings.Connection;
        await settings.OpenAsync();
        await connection.OpenAsync();
        await connection.DefaultStreamerName.FillAsync("settings_streamer");

        await connection.TestConnectionButton.ClickAsync();

        await Expect(connection.Result).ToHaveTextAsync(
            $"Connected to {scenario.Simulator.ApiBaseAddress} and loaded 3 queued song(s) and 2 history item(s) for settings_streamer.");
        // The test reads the channel with the environment's streamer token, the credential in use.
        var queue = await scenario.Simulator.WaitForFirstRequestAsync(ApiCalls.FetchQueue("settings_streamer"), cancellationToken);
        var history = await scenario.Simulator.WaitForFirstRequestAsync(ApiCalls.FetchPlayHistory("settings_streamer"), cancellationToken);
        Assert.Equal(($"Streamer {scenario.Simulator.AccessToken}", 200), (queue.Authorization, queue.StatusCode));
        Assert.Equal(($"Streamer {scenario.Simulator.AccessToken}", 200), (history.Authorization, history.StatusCode));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NoDefaultChannel_When_TheConnectionIsTested_Then_ItAsksForTheChannelWithoutCallingTheApi()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        var connection = settings.Connection;
        await settings.OpenAsync();
        await connection.OpenAsync();
        await Expect(connection.DefaultStreamerName).ToHaveValueAsync("");

        await connection.TestConnectionButton.ClickAsync();

        await Expect(connection.Result).ToHaveTextAsync("Enter a Default StreamerSongList Name before testing.");
        Assert.Empty(scenario.Simulator.Requests);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ARejectedApiToken_When_TheConnectionIsTested_Then_ItFailsAndRestoresThePreviousCredential()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("settings_streamer")
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var settings = scenario.Settings;
        var connection = settings.Connection;
        await settings.OpenAsync();
        await connection.OpenAsync();
        await connection.DefaultStreamerName.FillAsync("settings_streamer");
        await connection.ApiToken.FillAsync("wrong-token");

        await connection.TestConnectionButton.ClickAsync();

        // The simulator's 401 detail follows the client's message; ApiCredentialTest then puts the
        // environment's credential back.
        await Expect(connection.Result).ToHaveTextAsync(
            "Connection failed: StreamerSongList rejected the configured API token. invalid access token " +
            "The previous credential was restored.");
        await Expect(connection.CredentialStatus).ToHaveTextAsync("✓ API credential configured");
        var queue = await scenario.Simulator.WaitForFirstRequestAsync(ApiCalls.FetchQueue("settings_streamer"), cancellationToken);
        Assert.Equal(("Streamer wrong-token", 401), (queue.Authorization, queue.StatusCode));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASavedApiToken_When_ClearingIsConfirmed_Then_TheCredentialIsRemovedAndTheDashboardOpensSetup()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        // Without the environment's fallback credential, clearing the stored one leaves the app with none.
        await using var scenario = await AppScenario.StartAsync(cancellationToken, useEnvironmentCredential: false);
        var settings = await SaveSimulatorTokenAsync(scenario);
        var connection = settings.Connection;

        await connection.ClearCredentialButton.ClickAsync();
        var confirmation = settings.MessageBox("Clear API credential?");
        await confirmation.ExpectOpenAsync();
        await confirmation.ChooseAsync("Clear credential");

        await Expect(connection.Result).ToHaveTextAsync("API credential cleared. Other settings were not changed.");
        await Expect(connection.CredentialStatus).ToHaveTextAsync("No API credential configured");
        await settings.ClickDashboardLinkAsync();
        await Expect(scenario.Setup.Heading).ToBeVisibleAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASavedApiToken_When_ClearingIsDeclined_Then_TheCredentialStays()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken, useEnvironmentCredential: false);
        var settings = await SaveSimulatorTokenAsync(scenario);
        var connection = settings.Connection;

        await connection.ClearCredentialButton.ClickAsync();
        var confirmation = settings.MessageBox("Clear API credential?");
        await confirmation.ExpectOpenAsync();
        await confirmation.ChooseAsync("Keep credential");

        await Expect(connection.CredentialStatus).ToHaveTextAsync("✓ API credential configured");
        await Expect(connection.Result).ToHaveCountAsync(0);
    }

    /// <summary>Saves the simulator's token through Settings &gt; Connection on an app that had none.</summary>
    private static async Task<SettingsPage> SaveSimulatorTokenAsync(AppScenario scenario)
    {
        await Expect(scenario.Setup.Heading).ToBeVisibleAsync();
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Connection.OpenAsync();
        await Expect(settings.Connection.CredentialStatus).ToHaveTextAsync("No API credential configured");
        await settings.Connection.ApiToken.FillAsync(scenario.Simulator.AccessToken);
        await settings.SaveAsync();
        await Expect(settings.Connection.CredentialStatus).ToHaveTextAsync("✓ API credential configured");
        return settings;
    }
}
