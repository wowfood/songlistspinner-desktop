using System.Globalization;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class WinnerActionTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASpinWinner_When_MarkedPlayed_Then_TheSimulatorRecordsThePlayAndTheWheelDropsIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var page = scenario.App.Page;
        await page.LoadChannelAsync("demo");
        var queuedBefore = scenario.Channel.Queue.Count;
        var winner = await page.SpinAsync(scenario.Channel);
        var markPlayed = scenario.Simulator.WaitForRequestAsync(
            request => request.Method == "POST" && request.Path == "/queue/played",
            cancellationToken);

        await page.Locator("#markWinnerPlayedBtn").ClickAsync();

        await markPlayed;
        Assert.DoesNotContain(scenario.Channel.Queue, entry => entry.QueueId == winner.QueueId);
        Assert.Equal(winner.SongId, scenario.Channel.PlayHistory[0].SongId);
        await Expect(page.WinnerDialog()).ToBeHiddenAsync();
        await Expect(page.AvailableCount()).ToHaveTextAsync((queuedBefore - 1).ToString(CultureInfo.InvariantCulture));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheNowPlayingWorkflow_When_TheWinnerIsSetNowPlaying_Then_TheSimulatorAndDashboardShowIt()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var page = scenario.App.Page;
        await page.OpenSettingsAsync();
        await page.EnableNowPlayingWorkflowAsync();
        await page.OpenDashboardAsync();
        await page.LoadChannelAsync("demo");
        var winner = await page.SpinAsync(scenario.Channel);
        var setNowPlaying = scenario.Simulator.WaitForRequestAsync(
            request => request.Method == "POST" && request.Path == $"/queue/{winner.QueueId}/play",
            cancellationToken);

        await page.Locator("#setWinnerNowPlayingBtn").ClickAsync();

        await setNowPlaying;
        Assert.Equal(winner.QueueId, scenario.Channel.NowPlaying?.QueueId);
        await Expect(page.WinnerDialog()).ToBeHiddenAsync();
        await Expect(page.Locator("#nowPlayingSong")).ToContainTextAsync(winner.Title);
    }
}
