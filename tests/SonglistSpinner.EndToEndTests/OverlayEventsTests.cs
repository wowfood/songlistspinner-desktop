using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;

namespace SonglistSpinner.EndToEndTests;

public class OverlayEventsTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_AConnectedObsSource_When_TheWheelSpins_Then_ItReceivesTheChannelAndTheWinner()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var channel = await new ChannelSeed("overlay_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside, SongCatalog.Africa)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("overlay_streamer");
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        var initialState = await overlay.NextAsync("init_state", cancellationToken);

        var dialog = await dashboard.SpinAsync();

        Assert.Equal("overlay_streamer", initialState.GetProperty("streamer").GetString());
        var winner = channel.QueueEntryShowing(await dialog.ReadValuesAsync());
        var spin = await overlay.NextAsync("spin_command", cancellationToken);
        Assert.Equal(winner.QueueId, spin.GetProperty("winnerQueueId").GetInt32());
        var reveal = await overlay.NextAsync("winner_reveal", cancellationToken);
        Assert.Equal(
            [("Artist", winner.Artist), ("Title", winner.Title), ("Requester", winner.Requests[0].Requester)],
            reveal.GetProperty("fields").EnumerateArray()
                .Select(field => (field.GetProperty("label").GetString()!, field.GetProperty("value").GetString()!)));
        // The overlay shows the position the winner holds in the queue the API returns.
        Assert.Equal(channel.Queue.ToList().IndexOf(winner) + 1, reveal.GetProperty("queuePosition").GetInt32());
    }
}
