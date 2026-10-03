using System.Net;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using SonglistSpinner.Simulator;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>Spinning the wheel: the draw, the winner it reveals, and why a spin does not start.</summary>
public class SpinTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    private const string ApiError = "StreamerSongList returned HTTP 500 (Internal Server Error). database unavailable";

    [Fact(Timeout = 180_000)]
    public async Task Given_OneQueuedSong_When_Spun_Then_TheDialogShowsItAtPositionOneAfterADrawAndAPositionLookup()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await new ChannelSeed("spin_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("spin_streamer", scenario.Simulator, cancellationToken);
        var callsBeforeSpin = scenario.Simulator.Requests.Count;

        var dialog = await dashboard.SpinAsync();

        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        await Expect(dialog.QueuePosition).ToHaveTextAsync("#1");
        await Expect(dashboard.Status).ToHaveTextAsync("Winner: a-ha - Take On Me (synth_lover)");
        // The draw reads the queue and history together, then the position lookup reads the queue again.
        var spinCalls = scenario.Simulator.Requests.Skip(callsBeforeSpin).ToArray();
        Assert.Equal(3, spinCalls.Length);
        Assert.Equal("GET /play_history, GET /queue", string.Join(", ", spinCalls.Take(2).Select(Describe).Order()));
        Assert.Equal("GET /queue", Describe(spinCalls[2]));
        Assert.All(spinCalls, call => Assert.Equal("spin_streamer", call.Query["streamer_name"]));
        Assert.DoesNotContain(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ThreeQueuedSongs_When_Spun_Then_TheWinnerIsASeededEntryShownAtItsQueuePosition()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("three_song_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("three_song_streamer", scenario.Simulator, cancellationToken);

        var dialog = await dashboard.SpinAsync();

        var winner = channel.QueueEntryShowing(await dialog.ReadValuesAsync());
        var request = winner.Requests[0];
        await dialog.ExpectFieldsAsync(("Artist", winner.Artist), ("Title", winner.Title), ("Requester", request.Requester));
        var seededIndex = channel.Queue.ToList().FindIndex(entry => entry.QueueId == winner.QueueId);
        await Expect(dialog.QueuePosition).ToHaveTextAsync($"#{seededIndex + 1}");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheWheelHidden_When_ThePlayedListSpinIsPressed_Then_TheWinnerDialogShowsTheQueuedSong()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await new ChannelSeed("hidden_wheel_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("hidden_wheel_streamer", scenario.Simulator, cancellationToken);
        await dashboard.SetWheelVisibleAsync(false);
        await Expect(dashboard.WheelContents).ToBeHiddenAsync();
        await Expect(dashboard.PlayedListSpinButton).ToHaveTextAsync("SPIN");

        var dialog = await dashboard.SpinFromPlayedListAsync();

        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheQueueFetchFails_When_SpinIsPressed_Then_TheStatusShowsTheErrorAndNoWinnerIsShown()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        // A draw that ends at once leaves the app's one-second spin cooldown running, which a shared app would
        // carry into the next test's spin ("Cooldown active"), so this test starts its own.
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await new ChannelSeed("failing_spin_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("failing_spin_streamer", scenario.Simulator, cancellationToken);
        var callsBeforeSpin = scenario.Simulator.Requests.Count;
        scenario.Simulator.FailNextRequests(
            HttpMethod.Get,
            "/queue",
            HttpStatusCode.InternalServerError,
            "database unavailable");

        await dashboard.SpinButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync($"Error: {ApiError}");
        await Expect(dashboard.Health.Api).ToHaveTextAsync("Error");
        await Expect(dashboard.Winner.Root).ToBeHiddenAsync();
        var drawQueueCall = Assert.Single(
            scenario.Simulator.Requests.Skip(callsBeforeSpin),
            call => ApiCalls.FetchQueue("failing_spin_streamer")(call));
        Assert.Equal(500, drawQueueCall.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannelWithAnEmptyQueue_When_SpinIsPressed_Then_ThereIsNoWinnerAndTheWheelSaysSo()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        // A draw that ends at once leaves the app's one-second spin cooldown running, which a shared app would
        // carry into the next test's spin ("Cooldown active"), so this test starts its own.
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await new ChannelSeed("empty_queue_streamer").ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAndSettleAsync("empty_queue_streamer", scenario.Simulator, cancellationToken);

        await dashboard.SpinButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("No songs left to spin!");
        await Expect(dashboard.Winner.Root).ToBeHiddenAsync();
        await dashboard.ExpectWheelLabelsAsync("No songs in queue");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_NoChannelLoaded_When_SpinIsPressed_Then_TheStatusAsksForAStreamerAndNoApiCallIsMade()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("Not loaded");

        await dashboard.SpinButton.ClickAsync();

        await Expect(dashboard.Status).ToHaveTextAsync("Please enter a streamer name first");
        await Expect(dashboard.Winner.Root).ToBeHiddenAsync();
        Assert.Empty(scenario.Simulator.Requests);
    }

    private static string Describe(RecordedRequest request) => $"{request.Method} {request.Path}";
}
