using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class WinnerActionTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASpinWinner_When_MarkedPlayed_Then_TheSimulatorRecordsThePlayAndTheWheelDropsIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var dashboard = scenario.Dashboard;
        var channel = await new ChannelSeed("winner_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        await dashboard.LoadChannelAsync("winner_streamer");
        var dialog = await dashboard.SpinAsync();
        var winner = channel.QueueEntryShowing(await dialog.ReadValuesAsync());
        var runnerUp = channel.Queue.Single(entry => entry.QueueId != winner.QueueId);

        await dialog.MarkPlayedAsync();

        // The API was told to mark exactly that queue entry played, and nothing else changed the queue.
        var markPlayed = await scenario.Simulator.WaitForFirstRequestAsync(ApiCalls.MarkPlayed(winner.QueueId), cancellationToken);
        Assert.Equal(204, markPlayed.StatusCode);
        Assert.Single(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
        Assert.Equal(winner.SongId, Assert.Single(channel.Channel.PlayHistory).SongId);
        await dashboard.ExpectWheelLabelsAsync($"{runnerUp.Artist} - {runnerUp.Title} ({runnerUp.Requests[0].Requester})");
        await Expect(dashboard.QueuedCount).ToHaveTextAsync("1");
        await dashboard.PlayedList.ExpectLinesAsync($"Artist: {winner.Artist} | Title: {winner.Title}");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheNowPlayingWorkflow_When_TheOnlyQueuedSongIsSetNowPlaying_Then_TheSimulatorAndDashboardShowIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await settings.SaveAsync();
        var channel = await new ChannelSeed("now_playing_streamer")
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var takeOnMe = channel.QueueEntryFor(SongCatalog.TakeOnMe);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("now_playing_streamer");
        var dialog = await dashboard.SpinAsync();
        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));

        await dialog.SetNowPlayingAsync();

        var setNowPlaying = await scenario.Simulator.WaitForFirstRequestAsync(
            ApiCalls.SetNowPlaying(takeOnMe.QueueId),
            cancellationToken);
        Assert.Equal(204, setNowPlaying.StatusCode);
        Assert.Equal(takeOnMe.QueueId, channel.Channel.NowPlaying?.QueueId);
        await Expect(dashboard.NowPlayingSong).ToHaveTextAsync("Artist: a-ha | Title: Take On Me");
        await dashboard.ExpectWheelLabelsAsync("No songs in queue");
    }
}
