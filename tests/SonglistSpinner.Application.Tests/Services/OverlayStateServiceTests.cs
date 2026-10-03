using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.Winner;
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
        overlay.BroadcastWheelVisibility(false);
        overlay.BroadcastWinnerReveal([new WinnerDialogField("Title", "Winner")], 7);

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
        overlay.BroadcastWinnerReveal([new WinnerDialogField("Title", "Winner")], 7);
        overlay.BroadcastCloseWinner();

        using var state = await ReadInitialStateAsync(overlay);

        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("winner").ValueKind);
    }

    [Fact]
    public async Task Given_CollapsedPlayedListWithCustomWidth_When_OverlayReconnects_Then_InitialStateReplaysLayout()
    {
        var overlay = new OverlayStateService();
        overlay.UpdatePlayedListCollapsed(true);
        overlay.UpdatePlayedListWidth("320px", "200px");

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

        overlay.UpdateConfig(new SpinnerConfig());
        overlay.UpdatePlayedListCollapsed(true);
        overlay.UpdatePlayedListWidth("320px", "200px");
        overlay.BroadcastWheelVisibility(false);
        overlay.BroadcastWinnerReveal([new WinnerDialogField("Title", "Winner")], 7);
        overlay.BroadcastCloseWinner();

        var received = new List<string?>();
        for (var i = 0; i < 6; i++)
        {
            Assert.True(await events.MoveNextAsync());
            received.Add(events.Current.Name);
        }

        Assert.Equal(
            [
                OverlayEventNames.UpdateSongs,
                OverlayEventNames.SetCollapse,
                OverlayEventNames.SetPlayedListWidth,
                OverlayEventNames.SetWheelVisible,
                OverlayEventNames.WinnerReveal,
                OverlayEventNames.CloseWinner
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

        overlay.UpdateState(new SpinnerConfig(), [song], [], song, "streamer");

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
        Assert.True(events.Current.IsKeepAlive);
    }

    [Fact]
    public async Task Given_OverlayConnected_When_ItDisconnects_Then_EachConnectionChangeReportsTheClientCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        var reportedCounts = new List<int>();
        overlay.ConnectedClientsChanged += (_, _) => reportedCounts.Add(overlay.ConnectedClientCount);

        await using (var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken))
            Assert.True(await events.MoveNextAsync());

        Assert.Equal([1, 0], reportedCounts);
    }

    [Fact]
    public async Task Given_AKeepAliveWasSent_When_TheOverlayStateChanges_Then_TheOverlayStillReceivesTheChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var overlay = new OverlayStateService(time);
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());
        var keepAlive = events.MoveNextAsync().AsTask();
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.True(await keepAlive.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken));

        overlay.BroadcastCloseWinner();

        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15), cancellationToken));
        Assert.Equal(OverlayEventNames.CloseWinner, events.Current.Name);
    }

    [Fact]
    public async Task Given_AnOverlayThatStoppedReading_When_MoreChangesArriveThanItsBufferHolds_Then_ItLosesTheOldest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // The heartbeat never fires on this clock, so every event read is a broadcast.
        var overlay = new OverlayStateService(new FakeTimeProvider());
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync());

        for (var width = 1; width <= OverlayStateService.ClientBufferCapacity + 1; width++)
            overlay.UpdatePlayedListWidth($"{width}px", "");

        var widths = new List<string?>();
        for (var read = 0; read < OverlayStateService.ClientBufferCapacity; read++)
        {
            Assert.True(await events.MoveNextAsync());
            using var payload = ParseEventData(events.Current, OverlayEventNames.SetPlayedListWidth);
            widths.Add(payload.RootElement.GetProperty("width").GetString());
        }

        Assert.Equal(
            Enumerable.Range(2, OverlayStateService.ClientBufferCapacity).Select(width => $"{width}px"),
            widths);
    }

    [Fact]
    public async Task Given_AConnectionObserverThrows_When_AnOverlayConnects_Then_LaterObserversStillHearIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        var reportedCounts = new List<int>();
        overlay.ConnectedClientsChanged += (_, _) => throw new InvalidOperationException("Observer failed");
        overlay.ConnectedClientsChanged += (_, _) => reportedCounts.Add(overlay.ConnectedClientCount);
        await using var events = overlay.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        Assert.True(await events.MoveNextAsync());

        Assert.Equal([1], reportedCounts);
    }

    internal static async Task<JsonDocument> ReadInitialStateAsync(OverlayStateService overlay)
    {
        await using var events = overlay.SubscribeAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());

        return ParseEventData(events.Current, OverlayEventNames.InitialState);
    }

    internal static JsonDocument ParseEventData(OverlayEvent overlayEvent, string eventName)
    {
        Assert.Equal(eventName, overlayEvent.Name);
        Assert.NotNull(overlayEvent.Data);
        return JsonDocument.Parse(overlayEvent.Data);
    }

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
