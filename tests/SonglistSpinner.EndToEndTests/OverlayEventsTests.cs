using Xunit;

namespace SonglistSpinner.EndToEndTests;

public class OverlayEventsTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_AConnectedObsSource_When_TheWheelSpins_Then_ItReceivesTheChannelAndTheWinner()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var page = scenario.App.Page;
        await page.LoadChannelAsync("demo");
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        var initialState = await overlay.NextAsync("init_state", cancellationToken);

        var winner = await page.SpinAsync(scenario.Channel);

        Assert.Equal("demo", initialState.GetProperty("streamer").GetString());
        var spin = await overlay.NextAsync("spin_command", cancellationToken);
        Assert.Equal(winner.QueueId, spin.GetProperty("winnerQueueId").GetInt32());
        var reveal = await overlay.NextAsync("winner_reveal", cancellationToken);
        var revealedValues = reveal.GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("value").GetString())
            .ToList();
        Assert.Contains(winner.Title, revealedValues);
        Assert.Contains(winner.Artist, revealedValues);
    }
}
