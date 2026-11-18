using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class RealtimeQueueTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannel_When_AViewerRequestsASong_Then_TheDashboardShowsItWithoutARefresh()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("realtime_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("realtime_streamer");
        await Expect(dashboard.Health.Realtime).ToHaveTextAsync("Connected");
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        await overlay.NextAsync("init_state", cancellationToken);

        var request = await channel.Channel.RequestSongAsync("Rick Astley", "Never Gonna Give You Up", "late_viewer");

        await Expect(dashboard.QueuedCount).ToHaveTextAsync("3");
        await dashboard.ExpectWheelLabelsAsync(
            "a-ha - Take On Me (synth_lover)",
            "The Killers - Mr. Brightside (indie_kid)",
            "Rick Astley - Never Gonna Give You Up (late_viewer)");
        // The overlay mirrors the Dashboard's wheel, so the new request reaches OBS too.
        await overlay.NextAsync(
            "update_songs",
            update => update.GetProperty("wheelItems").EnumerateArray()
                .Any(item => item.TryGetProperty("queueId", out var queueId) && queueId.GetInt32() == request.QueueId),
            cancellationToken);
    }
}
