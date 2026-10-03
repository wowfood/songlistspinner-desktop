using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using SonglistSpinner.Simulator;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Simulator;

/// <summary>
/// The simulator's test controls that the end-to-end suite shares one simulator through: reset between tests, the
/// call-log wait, the dropped-connection fault and refused event connections.
/// </summary>
public class StreamerSongListSimulatorTests
{
    [Fact]
    public async Task Given_ARequestHeldByAnEarlierTest_When_TheSimulatorIsReset_Then_TheRequestIsAnsweredAndTheLogIsEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("held", "twitch");
        var hold = simulator.HoldNextRequest(HttpMethod.Get, "/streamers");
        using var http = CreateClient(simulator);
        var heldRequest = http.GetAsync("streamers?streamer_name=held&platform=twitch", cancellationToken);
        await hold.Arrived.WaitAsync(cancellationToken);

        simulator.Reset();

        // The channel went with the reset, so the released request finds no streamer.
        using var response = await heldRequest;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, simulator.EventConnectionCount);
        Assert.Equal(["/streamers"], simulator.Requests.Select(request => request.Path));
    }

    [Fact]
    public async Task Given_AMatchingRequestAlreadyAnswered_When_WaitingForTheFirstRequest_Then_ItCompletesWithThatRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("answered", "twitch");
        using var http = CreateClient(simulator);
        using var first = await http.GetAsync("queue?streamer_name=answered&platform=twitch", cancellationToken);
        using var second = await http.GetAsync($"queue?streamer_name=answered&platform=twitch&streamer_id={channel.StreamerId}", cancellationToken);

        var request = await simulator.WaitForFirstRequestAsync(
            candidate => candidate.Path == "/queue",
            cancellationToken);

        Assert.False(request.Query.ContainsKey("streamer_id"));
        Assert.Equal(2, simulator.RequestsTo(HttpMethod.Get, "/queue").Count);
    }

    [Fact]
    public async Task Given_ADroppedRequest_When_TheClientCalls_Then_TheConnectionClosesWithoutAResponseAndIsRecordedAsAborted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("dropped", "twitch");
        simulator.DropNextRequests(HttpMethod.Get, "/queue");
        using var http = CreateClient(simulator);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => http.GetAsync("queue?streamer_name=dropped&platform=twitch", cancellationToken));

        using var retry = await http.GetAsync("queue?streamer_name=dropped&platform=twitch", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal([true, false], simulator.RequestsTo(HttpMethod.Get, "/queue").Select(request => request.ConnectionAborted));
    }

    [Fact]
    public async Task Given_EventConnectionsRejected_When_AClientConnects_Then_ItIsRefusedUntilRejectionIsCleared()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.RejectEventConnections = true;
        using var refused = new ClientWebSocket();

        var failure = await Assert.ThrowsAsync<WebSocketException>(
            () => refused.ConnectAsync(simulator.EventsEndpoint, cancellationToken));

        Assert.Equal(WebSocketError.NotAWebSocket, failure.WebSocketErrorCode);
        simulator.RejectEventConnections = false;
        using var accepted = new ClientWebSocket();
        await accepted.ConnectAsync(simulator.EventsEndpoint, cancellationToken);
        Assert.Equal(WebSocketState.Open, accepted.State);
    }

    private static HttpClient CreateClient(StreamerSongListSimulator simulator)
    {
        var http = new HttpClient { BaseAddress = simulator.ApiBaseAddress };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Streamer", simulator.AccessToken);
        return http;
    }
}
