using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
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

    [Fact]
    public async Task Given_NoQueueYet_When_OverlayConnects_Then_InitialStateCarriesTheStateAndLayoutReplay()
    {
        var overlay = new OverlayStateService();

        using var state = await ReadInitialStateAsync(overlay);

        Assert.Equal(
            [
                "availableCount", "config", "nowPlayingText", "playedCount", "playedFieldTable",
                "playedListCollapsed", "playedListMinWidth", "playedListWidth", "playedTexts", "streamer",
                "wheelItems", "wheelVisible", "winner"
            ],
            PropertyNames(state.RootElement));
        var placeholder = Assert.Single(state.RootElement.GetProperty("wheelItems").EnumerateArray());
        Assert.Equal("""{"label":"Waiting for Dashboard..."}""", placeholder.GetRawText());
    }

    [Fact]
    public async Task Given_ConnectedOverlay_When_QueueIsUpdated_Then_UpdateSongsCarriesTheStateWithoutLayoutReplay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());
        var song = new SpinnerQueueItem
        {
            QueueId = 5,
            Song = new SpinnerSong { Artist = "Artist", Title = "Title" },
            Requests = [new SpinnerRequest { Name = "Viewer" }]
        };

        await overlay.UpdateStateAsync(new SpinnerConfig(), [song], [], song, "streamer");

        Assert.True(await events.MoveNextAsync());
        using var update = ParseEventData(events.Current, OverlayEventNames.UpdateSongs);
        var root = update.RootElement;
        Assert.Equal(
            [
                "availableCount", "config", "nowPlayingText", "playedCount", "playedFieldTable", "playedTexts",
                "streamer", "wheelItems"
            ],
            PropertyNames(root));
        var wheelItem = Assert.Single(root.GetProperty("wheelItems").EnumerateArray());
        Assert.Equal(5, wheelItem.GetProperty("queueId").GetInt32());
        Assert.Equal("Artist - Title (Viewer)", wheelItem.GetProperty("label").GetString());
        Assert.Equal("Artist: Artist | Title: Title", root.GetProperty("nowPlayingText").GetString());
        Assert.Equal("streamer", root.GetProperty("streamer").GetString());
        Assert.Equal(1, root.GetProperty("availableCount").GetInt32());
    }

    [Fact]
    public async Task Given_ConnectedOverlay_When_FifteenQuietSecondsPass_Then_OverlayReceivesAKeepAliveComment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var overlay = new OverlayStateService(time);
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());
        // The subscription starts its heartbeat delay before MoveNextAsync returns.
        var next = events.MoveNextAsync().AsTask();

        time.Advance(TimeSpan.FromSeconds(15));

        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken));
        Assert.Equal(": keep-alive\n\n", events.Current);
    }

    internal static async Task<JsonDocument> ReadInitialStateAsync(OverlayStateService overlay)
    {
        await using var events = overlay.SubscribeAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());

        return ParseEventData(events.Current, OverlayEventNames.InitialState);
    }

    private static JsonDocument ParseEventData(string message, string eventName)
    {
        const string dataPrefix = "\ndata: ";
        Assert.StartsWith("event: " + eventName + dataPrefix, message);
        return JsonDocument.Parse(message[(message.IndexOf(dataPrefix, StringComparison.Ordinal) + dataPrefix.Length)..]);
    }

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
