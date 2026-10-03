using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The winner dialog a spin opens: which action it suggests and focuses, and the fields and queue position the
/// Winner Dialog settings show.
/// </summary>
public class WinnerDialogTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_DefaultSettings_When_ASpinEnds_Then_LeaveInQueueIsThePrimaryFocusedActionAndSetNowPlayingIsAbsent()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await new ChannelSeed("default_dialog_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("default_dialog_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectPrimaryActionAsync(dialog.LeaveInQueueButton);
        await dialog.ExpectSecondaryActionAsync(dialog.MarkPlayedButton);
        await Expect(dialog.SetNowPlayingButton).ToHaveCountAsync(0);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PreferMarkPlayedSaved_When_ASpinEnds_Then_MarkPlayedIsThePrimaryFocusedAction()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.PreferMarkPlayed.CheckAsync();
        await settings.SaveAsync();
        await new ChannelSeed("prefer_played_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("prefer_played_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectPrimaryActionAsync(dialog.MarkPlayedButton);
        await dialog.ExpectSecondaryActionAsync(dialog.LeaveInQueueButton);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheNowPlayingWorkflowSaved_When_ASpinEnds_Then_SetNowPlayingIsThePrimaryFocusedAction()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await settings.SaveAsync();
        await new ChannelSeed("workflow_dialog_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("workflow_dialog_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectPrimaryActionAsync(dialog.SetNowPlayingButton);
        await dialog.ExpectSecondaryActionAsync(dialog.MarkPlayedButton);
        await dialog.ExpectSecondaryActionAsync(dialog.LeaveInQueueButton);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_SpinnerSettingsOpen_When_TheNowPlayingWorkflowIsChecked_Then_PreferMarkPlayedIsDisabled()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await Expect(settings.Spinner.PreferMarkPlayed).ToBeEnabledAsync();

        await settings.Spinner.NowPlayingWorkflow.CheckAsync();

        await Expect(settings.Spinner.PreferMarkPlayed).ToBeDisabledAsync();
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDonationWinnerFieldSaved_When_ATippedRequestWins_Then_TheDialogShowsItsDonationLast()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveWinnerDonationFieldAsync(scenario);
        await new ChannelSeed("tipped_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("tipped_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectFieldsAsync(
            ("Artist", "a-ha"),
            ("Title", "Take On Me"),
            ("Requester", "synth_lover"),
            ("Donation", "3.50"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDonationWinnerFieldSaved_When_AnUntippedRequestWins_Then_TheDialogShowsDonationNone()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveWinnerDonationFieldAsync(scenario);
        await new ChannelSeed("untipped_streamer").WithQueued(SongCatalog.MrBrightside).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("untipped_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectFieldsAsync(
            ("Artist", "The Killers"),
            ("Title", "Mr. Brightside"),
            ("Requester", "indie_kid"),
            ("Donation", "None"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ShowQueuePositionCleared_When_Spun_Then_TheDialogHasNoPositionAndTheQueueIsReadOnce()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.WinnerShowQueuePosition.UncheckAsync();
        await settings.SaveAsync();
        await new ChannelSeed("no_position_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("no_position_streamer", scenario.Simulator, cancellationToken);
        var callsBeforeSpin = scenario.Simulator.Requests.Count;

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        await Expect(dialog.QueuePosition).ToHaveCountAsync(0);
        // Only the draw read the queue: no position lookup followed it.
        Assert.Single(
            scenario.Simulator.Requests.Skip(callsBeforeSpin),
            call => ApiCalls.FetchQueue("no_position_streamer")(call));
    }

    private static async Task SaveWinnerDonationFieldAsync(AppScenario scenario)
    {
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.WinnerFields.SetSelectedAsync("Donation", true);
        await settings.SaveAsync();
    }
}
