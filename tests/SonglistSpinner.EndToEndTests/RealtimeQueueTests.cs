using System.Globalization;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class RealtimeQueueTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ALoadedChannel_When_AViewerRequestsASong_Then_TheDashboardShowsItWithoutARefresh()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var page = scenario.App.Page;
        await page.LoadChannelAsync("demo");
        await page.WaitForRealtimeAsync();
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        await overlay.NextAsync("init_state", cancellationToken);
        var queuedBefore = scenario.Channel.Queue.Count;

        var request = await scenario.Channel.RequestSongAsync("Rick Astley", "Never Gonna Give You Up", "late_viewer");

        await Expect(page.AvailableCount()).ToHaveTextAsync((queuedBefore + 1).ToString(CultureInfo.InvariantCulture));
        // The overlay mirrors the Dashboard's wheel, so the new request reaches OBS too.
        await overlay.NextAsync(
            "update_songs",
            update => update.GetProperty("wheelItems").EnumerateArray()
                .Any(item => item.TryGetProperty("queueId", out var queueId) && queueId.GetInt32() == request.QueueId),
            cancellationToken);
    }
}
