using System.Net;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Simulator;
using Xunit;
using static SonglistSpinner.IntegrationTests.SimulatorClients;

namespace SonglistSpinner.IntegrationTests.StreamerSongList.Api.V2;

public class StreamerSongListApiClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ChannelOnYouTube_When_ResolveStreamerAsync_Then_ReturnsItsStreamerIdAndPlatformIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("SomeStreamer", "youtube", streamerId: 314);
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now));

        var streamer = await client.ResolveStreamerAsync(
            new StreamerSongListChannel("somestreamer", "YouTube"),
            cancellationToken);

        Assert.Equal(new StreamerId(314), streamer.Id);
        var identity = Assert.Single(streamer.Platforms);
        Assert.Equal(StreamerSongListPlatformNames.YouTube, identity.Platform);
        Assert.Equal("SomeStreamer", identity.Username);
    }

    [Fact]
    public async Task Given_QueueWithNowPlaying_When_FetchQueueSnapshotAsync_Then_MapsUpcomingEntriesInOrderAndThePlayingEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood");
        var playing = await channel.RequestSongAsync("Fleetwood Mac", "Dreams", "night_owl");
        await channel.SetNowPlayingAsync(playing.QueueId);
        var first = await channel.RequestSongAsync("a-ha", "Take On Me", "synth_lover", 3.50m);
        var second = await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now));

        var snapshot = await client.FetchQueueSnapshotAsync(new StreamerSongListChannel("wowfood"), cancellationToken);

        Assert.Equal(
            [(first.QueueId, 1, "a-ha", "Take On Me"), (second.QueueId, 2, "Toto", "Africa")],
            snapshot.Items.Select(item => (item.QueueId, item.Position, item.Song.Artist, item.Song.Title)));
        var request = Assert.Single(snapshot.Items[0].Requests);
        Assert.Equal("synth_lover", request.Name);
        Assert.Equal(3.50m, request.Amount);
        Assert.Equal(first.SongId, snapshot.Items[0].Song.Id);
        Assert.Equal(playing.QueueId, snapshot.Playing?.QueueId);
        Assert.Equal("Dreams", snapshot.Playing?.Song.Title);
    }

    [Fact]
    public async Task Given_PlaysOverTwoMonths_When_FetchPlayHistoryAsyncForTheWeek_Then_ReturnsOnlyThisWeeksPlaysNewestFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood");
        await channel.AddPlayedSongAsync("Toto", "Africa", Now.AddDays(-40));
        await channel.AddPlayedSongAsync("Queen", "Don't Stop Me Now", Now.AddDays(-3));
        await channel.AddPlayedSongAsync("ABBA", "Dancing Queen", Now.AddDays(-8));
        await channel.AddPlayedSongAsync("Daft Punk", "Get Lucky", Now.AddHours(-1), "early_bird", 5.25m);
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now));

        var history = await client.FetchPlayHistoryAsync(
            new StreamerSongListChannel("wowfood"),
            SpinnerSettingValues.PlayHistoryPeriods.Week,
            cancellationToken);

        Assert.Equal(["Get Lucky", "Don't Stop Me Now"], history.Select(item => item.Song?.Title));
        var request = Assert.Single(history[0].Requests);
        Assert.Equal("early_bird", request.Name);
        Assert.Equal(5.25m, request.DonationAmount);
    }

    [Fact]
    public async Task Given_MorePlaysThanThePageSize_When_FetchPlayHistoryAsync_Then_ReturnsOnlyTheNewestPage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        var channel = simulator.AddChannel("wowfood");
        await channel.AddPlayedSongAsync("Artist", "Oldest", Now.AddHours(-3));
        await channel.AddPlayedSongAsync("Artist", "Newest", Now.AddHours(-1));
        await channel.AddPlayedSongAsync("Artist", "Middle", Now.AddHours(-2));
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now), pageSize: 2);

        var history = await client.FetchPlayHistoryAsync(
            new StreamerSongListChannel("wowfood"),
            SpinnerSettingValues.PlayHistoryPeriods.All,
            cancellationToken);

        Assert.Equal(["Newest", "Middle"], history.Select(item => item.Song?.Title));
    }

    [Fact]
    public async Task Given_WrongToken_When_FetchQueueSnapshotAsync_Then_ThrowsUnauthorizedWithTheApiDetail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("wowfood");
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now), token: "not-the-token");

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(new StreamerSongListChannel("wowfood"), cancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(
            "StreamerSongList rejected the configured API token. invalid access token",
            exception.Message);
    }

    [Fact]
    public async Task Given_RateLimited_When_FetchQueueSnapshotAsync_Then_ThrowsTooManyRequestsWithTheApiDetail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("wowfood");
        simulator.FailNextRequests(HttpMethod.Get, "/queue", HttpStatusCode.TooManyRequests, "slow down");
        using var http = new HttpClient();
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(new StreamerSongListChannel("wowfood"), cancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(
            "StreamerSongList rate-limited the request. Try again shortly. slow down",
            exception.Message);
    }

    [Fact]
    public async Task Given_TheApiStopsResponding_When_TheHttpClientTimeoutExpires_Then_ThrowsTaskCanceledException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        simulator.AddChannel("wowfood");
        var hold = simulator.HoldNextRequest(HttpMethod.Get, "/queue");
        // The response is held until the test ends, so the timeout always expires first.
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };
        var client = CreateApiClient(simulator, http, new FakeTimeProvider(Now));

        var fetch = client.FetchQueueSnapshotAsync(new StreamerSongListChannel("wowfood"), cancellationToken);

        await hold.Arrived.WaitAsync(WaitLimit, cancellationToken);
        await Assert.ThrowsAsync<TaskCanceledException>(() => fetch.WaitAsync(WaitLimit, cancellationToken));
    }
}
