using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>The winner reveal an OBS browser source shows after a Dashboard spin.</summary>
public class OverlayWinnerRevealTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_AnOpenOverlayAndASingleSongQueue_When_TheWheelSpins_Then_TheOverlayRevealsThatSongAtPositionOne()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("reveal_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("reveal_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("reveal_streamer");

        await dashboard.SpinAsync();

        await overlay.ExpectWinnerFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        await Expect(overlay.WinnerQueuePosition).ToHaveTextAsync("#1");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AWinnerDialogWidthFontAndSizeSaved_When_TheWheelSpins_Then_TheOverlayRevealCardUsesThem()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        await settings.OverlayLayout.SetWinnerDialogStyleAsync("30rem", "serif", "1.25rem");
        await settings.SaveAsync();
        await new ChannelSeed("reveal_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.OpenAsync();
        await dashboard.LoadChannelAsync("reveal_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("reveal_streamer");

        await dashboard.SpinAsync();

        await overlay.ExpectWinnerFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        // 30rem and 1.25rem at the overlay's 16px root size.
        await Expect(overlay.WinnerCard).ToHaveCSSAsync("width", "480px");
        await Expect(overlay.WinnerCard).ToHaveCSSAsync("font-family", "serif");
        await Expect(overlay.WinnerCard).ToHaveCSSAsync("font-size", "20px");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AWinnerRevealedInTheOverlay_When_LeaveInQueueIsChosen_Then_TheOverlayClosesTheReveal()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("reveal_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("reveal_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("reveal_streamer");
        var dialog = await dashboard.SpinAsync();
        await overlay.ExpectWinnerFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));

        await dialog.LeaveInQueueAsync();

        await Expect(overlay.Winner).ToBeHiddenAsync();
        // Leaving the winner in the queue changes nothing at StreamerSongList, so the wheel keeps the song.
        Assert.DoesNotContain(scenario.Simulator.Requests, ApiCalls.IsQueueChange);
        await overlay.ExpectWheelLabelsAsync("a-ha - Take On Me (synth_lover)");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_AWinnerDialogOpenOnTheDashboard_When_AnOverlayJoinsLate_Then_ItShowsTheSameWinner()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("reveal_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("reveal_streamer");
        var dialog = await dashboard.SpinAsync();
        await dialog.ExpectFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        // The status changes after the reveal is sent to overlays, so the reveal is in the state a new overlay gets.
        await Expect(dashboard.Status).ToHaveTextAsync("Winner: a-ha - Take On Me (synth_lover)");

        var overlay = await scenario.OpenOverlayAsync();

        await overlay.ExpectWinnerFieldsAsync(("Artist", "a-ha"), ("Title", "Take On Me"), ("Requester", "synth_lover"));
        await Expect(overlay.WinnerQueuePosition).ToHaveTextAsync("#1");
    }
}
