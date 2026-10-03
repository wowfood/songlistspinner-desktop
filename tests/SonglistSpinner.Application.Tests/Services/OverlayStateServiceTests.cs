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
