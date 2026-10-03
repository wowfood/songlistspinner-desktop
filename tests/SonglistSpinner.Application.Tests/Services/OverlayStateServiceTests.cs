using System.Text.Json;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class OverlayStateServiceTests
{
    [Fact]
    public async Task Given_NoVisibilityBroadcast_When_OverlayConnects_Then_InitialStateShowsWheelWithoutWinner()
    {
        var overlay = new OverlayStateService();

        using var state = await ReadInitialStateAsync(overlay);

        Assert.True(state.RootElement.GetProperty("wheelVisible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("winner").ValueKind);
    }

    [Fact]
    public async Task Given_HiddenWheelAndRevealedWinner_When_OverlayReconnects_Then_InitialStateReplaysBoth()
    {
        var overlay = new OverlayStateService();
        await overlay.BroadcastWheelVisibilityAsync(false);
        await overlay.BroadcastWinnerRevealAsync([new WinnerDialogField("Title", "Winner")], 7);

        using var state = await ReadInitialStateAsync(overlay);

        var winner = state.RootElement.GetProperty("winner");
        Assert.False(state.RootElement.GetProperty("wheelVisible").GetBoolean());
        Assert.Equal(7, winner.GetProperty("queuePosition").GetInt32());
        var field = Assert.Single(winner.GetProperty("fields").EnumerateArray());
        Assert.Equal("Title", field.GetProperty("label").GetString());
        Assert.Equal("Winner", field.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Given_WinnerClosed_When_OverlayReconnects_Then_InitialStateHasNoWinner()
    {
        var overlay = new OverlayStateService();
        await overlay.BroadcastWinnerRevealAsync([new WinnerDialogField("Title", "Winner")], 7);
        await overlay.BroadcastCloseWinnerAsync();

        using var state = await ReadInitialStateAsync(overlay);

        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("winner").ValueKind);
    }

    [Fact]
    public async Task Given_CollapsedPlayedListWithCustomWidth_When_OverlayReconnects_Then_InitialStateReplaysLayout()
    {
        var overlay = new OverlayStateService();
        await overlay.UpdatePlayedListCollapsedAsync(true);
        await overlay.UpdatePlayedListWidthAsync("320px", "200px");

        using var state = await ReadInitialStateAsync(overlay);

        Assert.True(state.RootElement.GetProperty("playedListCollapsed").GetBoolean());
        Assert.Equal("320px", state.RootElement.GetProperty("playedListWidth").GetString());
        Assert.Equal("200px", state.RootElement.GetProperty("playedListMinWidth").GetString());
    }

    [Fact]
    public async Task Given_ConnectedOverlay_When_OverlayStateChanges_Then_OverlayReceivesEachChangeInCallOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());

        await overlay.UpdateConfigAsync(new SpinnerConfig());
        await overlay.UpdatePlayedListCollapsedAsync(true);
        await overlay.UpdatePlayedListWidthAsync("320px", "200px");
        await overlay.BroadcastWheelVisibilityAsync(false);
        await overlay.BroadcastWinnerRevealAsync([new WinnerDialogField("Title", "Winner")], 7);
        await overlay.BroadcastCloseWinnerAsync();

        var received = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            Assert.True(await events.MoveNextAsync());
            received.Add(events.Current[..events.Current.IndexOf('\n', StringComparison.Ordinal)]);
        }

        Assert.Equal(
            [
                "event: " + OverlayEventNames.UpdateSongs,
                "event: " + OverlayEventNames.SetCollapse,
                "event: " + OverlayEventNames.SetPlayedListWidth,
                "event: " + OverlayEventNames.SetWheelVisible,
                "event: " + OverlayEventNames.WinnerReveal,
                "event: " + OverlayEventNames.CloseWinner
            ],
            received);
    }

    internal static async Task<JsonDocument> ReadInitialStateAsync(OverlayStateService overlay)
    {
        await using var events = overlay.SubscribeAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());

        const string dataPrefix = "\ndata: ";
        var message = events.Current;
        Assert.StartsWith("event: init_state" + dataPrefix, message);
        return JsonDocument.Parse(message[(message.IndexOf(dataPrefix, StringComparison.Ordinal) + dataPrefix.Length)..]);
    }
}
