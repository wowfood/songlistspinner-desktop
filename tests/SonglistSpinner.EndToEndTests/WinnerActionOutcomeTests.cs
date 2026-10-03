using System.Net;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using SonglistSpinner.Simulator;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// What each winner action does to StreamerSongList and the Dashboard: Leave in Queue (or Escape), Mark Played and
/// Set Now Playing, and a failed action, which keeps the dialog open with the reason.
/// </summary>
public class WinnerActionOutcomeTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string ApiError = "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable";

    [Fact(Timeout = 180_000)]
    public async Task Given_AWinner_When_LeftInTheQueue_Then_NothingIsPostedAndTheWheelKeepsEverySeededSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("leave_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("leave_streamer", scenario.Simulator, cancellationToken);
        var dialog = await dashboard.SpinAsync();

        await dialog.LeaveInQueueAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Winner left in the queue.");
        await dashboard.ExpectWheelLabelsAsync(SongCatalog.TakeOnMe.WheelLabel, SongCatalog.MrBrightside.WheelLabel);
        Assert.DoesNotContain(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
        Assert.Equal(QueueIds(channel.Queue), QueueIds(channel.Channel.Queue));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AWinner_When_EscapeIsPressed_Then_TheWinnerIsLeftInTheQueueWithoutAPost()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("escape_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("escape_streamer", scenario.Simulator, cancellationToken);
        var dialog = await dashboard.SpinAsync();

        await dialog.EscapeAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Winner left in the queue.");
        Assert.DoesNotContain(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
        Assert.Equal(QueueIds(channel.Queue), QueueIds(channel.Channel.Queue));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheOnlyQueuedSongWins_When_MarkedPlayed_Then_ItIsPostedOnceAndMovesFromTheWheelToThePlayedList()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("mark_played_streamer")
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var takeOnMe = channel.QueueEntryFor(SongCatalog.TakeOnMe);
        await dashboard.LoadChannelAndSettleAsync("mark_played_streamer", scenario.Simulator, cancellationToken);
        var dialog = await dashboard.SpinAsync();

        await dialog.MarkPlayedAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Winner marked as played.");
        await dashboard.PlayedList.ExpectLinesAsync("Artist: a-ha | Title: Take On Me");
        await Expect(dashboard.PlayedCount).ToHaveTextAsync("1");
        await dashboard.ExpectWheelLabelsAsync("No songs in queue");
        await Expect(dashboard.QueueEmptyState).ToHaveTextAsync("No eligible queued songs are available to spin.");
        var markPlayed = Assert.Single(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
        Assert.True(ApiCalls.MarkPlayed(takeOnMe.QueueId)(markPlayed));
        Assert.Equal(204, markPlayed.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASongIsPlayingAndTheWorkflowIsOn_When_TheWinnerIsSetNowPlaying_Then_ItIsPromotedAndShownAsNowPlaying()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveNowPlayingWorkflowAsync(scenario);
        var channel = await new ChannelSeed("promote_streamer")
            .WithNowPlaying(SongCatalog.Dreams)
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var takeOnMe = channel.QueueEntryFor(SongCatalog.TakeOnMe);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("promote_streamer", scenario.Simulator, cancellationToken);
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: Fleetwood Mac | Title: Dreams");
        var dialog = await dashboard.SpinAsync();
        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));

        await dialog.SetNowPlayingAsync();

        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: a-ha | Title: Take On Me");
        var promote = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.SetNowPlaying(takeOnMe.QueueId),
            cancellationToken);
        Assert.Equal(204, promote.StatusCode);
        Assert.Equal(takeOnMe.QueueId, channel.Channel.NowPlaying?.QueueId);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_MarkPlayedFailsOnTheServer_When_TheWinnerIsMarkedPlayed_Then_TheDialogStaysOpenWithTheReason()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("failed_mark_streamer")
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var takeOnMe = channel.QueueEntryFor(SongCatalog.TakeOnMe);
        await dashboard.LoadChannelAndSettleAsync("failed_mark_streamer", scenario.Simulator, cancellationToken);
        scenario.Simulator.FailNextRequests(
            HttpMethod.Post,
            "/queue/played",
            HttpStatusCode.InternalServerError,
            "database unavailable");
        var dialog = await dashboard.SpinAsync();

        await dialog.ClickMarkPlayedAsync();

        const string error = $"StreamerSongList failed while marking the winner played: {ApiError}";
        await Expect(dialog.Error).ToHaveTextAsync(error);
        await Expect(dialog.Root).ToBeVisibleAsync();
        await Expect(dashboard.Status).ToHaveTextAsync(error);
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        var markPlayed = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.MarkPlayed(takeOnMe.QueueId),
            cancellationToken);
        Assert.Equal(500, markPlayed.StatusCode);
        Assert.Equal(QueueIds(channel.Queue), QueueIds(channel.Channel.Queue));
        await dialog.LeaveInQueueAsync();
        await Expect(dashboard.Status).ToHaveTextAsync("Winner left in the queue.");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PromotionFailsOnTheServer_When_TheWinnerIsSetNowPlaying_Then_TheDialogStaysOpenWithTheReason()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await SaveNowPlayingWorkflowAsync(scenario);
        var channel = await new ChannelSeed("failed_promote_streamer")
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var takeOnMe = channel.QueueEntryFor(SongCatalog.TakeOnMe);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAndSettleAsync("failed_promote_streamer", scenario.Simulator, cancellationToken);
        scenario.Simulator.FailNextRequests(
            HttpMethod.Post,
            $"/queue/{takeOnMe.QueueId}/play",
            HttpStatusCode.InternalServerError,
            "database unavailable");
        var dialog = await dashboard.SpinAsync();

        await dialog.ClickSetNowPlayingAsync();

        await Expect(dialog.Error).ToHaveTextAsync($"StreamerSongList failed while updating Now Playing: {ApiError}");
        await Expect(dialog.Root).ToBeVisibleAsync();
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        var promote = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.SetNowPlaying(takeOnMe.QueueId),
            cancellationToken);
        Assert.Equal(500, promote.StatusCode);
        Assert.Null(channel.Channel.NowPlaying);
    }

    private static async Task SaveNowPlayingWorkflowAsync(AppScenario scenario)
    {
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await settings.SaveAsync();
    }

    private static int[] QueueIds(IEnumerable<SimulatedQueueEntry> queue) =>
        [.. queue.Select(entry => entry.QueueId)];
}
