using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api;

public class StreamerSongListApiClientTests
{
    [Fact]
    public void Given_DefaultOptions_When_ReadingBaseAddress_Then_UsesProductionApiEndpoint()
    {
        var options = new StreamerSongListApiOptions();

        Assert.Equal("https://api.streamersonglist.com/", options.BaseAddress.AbsoluteUri);
    }

    [Fact]
    public async Task Given_Channel_When_ResolveStreamerIdAsync_Then_UsesStreamerLookupAndReturnsId()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":314}"""));
        var client = CreateClient(handler);

        var streamerId = await client.ResolveStreamerIdAsync(
            new StreamerSongListChannel("Foo Bar", "YOUTUBE"),
            TestContext.Current.CancellationToken);

        Assert.Equal(314, streamerId);
        Assert.Equal(
            "https://example.test/streamers?streamer_name=Foo%20Bar&platform=youtube",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Streamer", handler.Authorization?.Scheme);
    }

    [Fact]
    public async Task Given_LinkedPlatforms_When_ResolveStreamerAsync_Then_MapsAvailableIdentities()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "id": 314,
              "platforms": {
                "twitch": { "username": "wowfood", "platformID": "tw-1" },
                "youtube": { "username": "Wow Food", "platformID": "yt-2" },
                "kick": null,
                "none": { "username": "wowfood", "platformID": "ssl-3" }
              }
            }
            """));
        var client = CreateClient(handler);

        var streamer = await client.ResolveStreamerAsync(
            new StreamerSongListChannel("wowfood", "twitch"),
            TestContext.Current.CancellationToken);

        Assert.Equal(314, streamer.Id);
        Assert.Collection(
            streamer.Platforms,
            twitch =>
            {
                Assert.Equal("twitch", twitch.Platform);
                Assert.Equal("wowfood", twitch.Username);
                Assert.Equal("tw-1", twitch.PlatformId);
            },
            youtube =>
            {
                Assert.Equal("youtube", youtube.Platform);
                Assert.Equal("Wow Food", youtube.Username);
                Assert.Equal("yt-2", youtube.PlatformId);
            },
            native =>
            {
                Assert.Equal("none", native.Platform);
                Assert.Equal("wowfood", native.Username);
                Assert.Equal("ssl-3", native.PlatformId);
            });
    }

    [Fact]
    public async Task Given_InvalidStreamerResponse_When_ResolveStreamerIdAsync_Then_RejectsId()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":0}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.ResolveStreamerIdAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Contains("invalid streamer ID", exception.Message);
    }

    [Fact]
    public async Task Given_StreamerCredential_When_FetchQueueAsync_Then_UsesV2QueryAndMapsItems()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [{
                "id": 91,
                "position": 4,
                "nonlistSong": "",
                "requests": [{
                  "amount": 12.50,
                  "name": "",
                  "user": { "username": "viewer" }
                }],
                "song": { "artist": "Artist", "title": "Title" },
                "songId": 42
              }],
              "playing": null,
              "total": 1
            }
            """));
        var client = CreateClient(handler, StreamerSongListCredentialKind.Streamer);

        var result = await client.FetchQueueAsync(
            new StreamerSongListChannel("Foo Bar", "TWITCH"),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(91, item.QueueId);
        Assert.Equal(4, item.Position);
        Assert.Equal(42, item.Song.Id);
        Assert.Equal("Artist", item.Song.Artist);
        Assert.Equal("Title", item.Song.Title);
        Assert.Equal("viewer", Assert.Single(item.Requests).Name);
        Assert.Equal(12.50m, item.Requests[0].Amount);
        Assert.Equal("https://example.test/queue?streamer_name=Foo%20Bar&platform=twitch", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Streamer", handler.Authorization?.Scheme);
        Assert.Equal("test-token", handler.Authorization?.Parameter);
    }

    [Fact]
    public async Task Given_NowPlayingItem_When_FetchQueueSnapshotAsync_Then_MapsPlayingAndUpcomingItems()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [{
                "id": 91,
                "position": 1,
                "requests": [],
                "song": { "artist": "Queued Artist", "title": "Queued Song" },
                "songId": 42
              }],
              "playing": {
                "id": 77,
                "requests": [{ "amount": null, "name": "viewer", "user": null }],
                "song": { "artist": "Playing Artist", "title": "Playing Song" },
                "songId": 31
              },
              "total": 1
            }
            """));
        var client = CreateClient(handler);

        var result = await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Equal(91, Assert.Single(result.Items).QueueId);
        Assert.NotNull(result.Playing);
        Assert.Equal(77, result.Playing.QueueId);
        Assert.Equal(31, result.Playing.Song.Id);
        Assert.Equal("Playing Artist", result.Playing.Song.Artist);
        Assert.Equal("Playing Song", result.Playing.Song.Title);
        Assert.Equal("viewer", Assert.Single(result.Playing.Requests).Name);
    }

    [Fact]
    public async Task Given_OAuthCredential_When_FetchQueueAsync_Then_AddsBearerAndClientIdHeaders()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":[],"playing":null,"total":0}"""));
        var client = CreateClient(handler, StreamerSongListCredentialKind.OAuthBearer, "desktop-client");

        await client.FetchQueueAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("desktop-client", handler.ClientId);
    }

    [Fact]
    public async Task Given_NullRequestCollection_When_FetchQueueAsync_Then_MapsEmptyRequests()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [{
                "id": 92,
                "position": 1,
                "nonlistSong": "",
                "requests": null,
                "song": { "artist": "Artist", "title": "Unrequested Song" },
                "songId": 43
              }],
              "playing": null,
              "total": 1
            }
            """));
        var client = CreateClient(handler);

        var result = await client.FetchQueueAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(result).Requests);
    }

    [Fact]
    public async Task Given_NullItems_When_FetchQueueAsync_Then_ReturnsEmptyQueue()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":null,"playing":null,"total":0}"""));
        var client = CreateClient(handler);

        var result = await client.FetchQueueAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Given_UserCredential_When_FetchQueueAsync_Then_UsesUserAuthorizationScheme()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":[],"playing":null,"total":0}"""));
        var client = CreateClient(handler, StreamerSongListCredentialKind.User);

        await client.FetchQueueAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Equal("User", handler.Authorization?.Scheme);
        Assert.Null(handler.ClientId);
    }

    [Fact]
    public async Task Given_QueueId_When_MarkQueueItemAsPlayedAsync_Then_PostsQueueId()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.MarkQueueItemAsPlayedAsync(91, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/queue/played?queue_id=91", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Streamer", handler.Authorization?.Scheme);
    }

    [Fact]
    public async Task Given_InvalidQueueId_When_MarkQueueItemAsPlayedAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.MarkQueueItemAsPlayedAsync(0, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_StreamerId_When_MarkNowPlayingAsPlayedAsync_Then_PostsPlayingPosition()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.MarkNowPlayingAsPlayedAsync(314, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://example.test/queue/played?position=playing&streamer_id=314",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Given_QueueId_When_PromoteQueueItemToNowPlayingAsync_Then_PostsPlayAction()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.PromoteQueueItemToNowPlayingAsync(91, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/queue/91/play", handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Given_InvalidStreamerId_When_MarkNowPlayingAsPlayedAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.MarkNowPlayingAsPlayedAsync(0, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_WeekPeriod_When_FetchPlayHistoryAsync_Then_UsesV2FiltersAndMapsDonation()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [{
                "donationAmount": 5.25,
                "requests": [{ "amount": null, "name": "requester", "user": null }],
                "song": { "artist": "Artist", "title": "Played Song" },
                "songId": 7
              }],
              "token": "next-cursor",
              "total": 1
            }
            """));
        var now = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
        var client = CreateClient(handler, timeProvider: new FixedTimeProvider(now));

        var result = await client.FetchPlayHistoryAsync(
            new StreamerSongListChannel("wowfood", "youtube"),
            "week",
            TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(7, item.Song?.Id);
        Assert.Equal(5.25m, Assert.Single(item.Requests).DonationAmount);
        var query = Uri.UnescapeDataString(handler.RequestUri?.Query ?? "");
        Assert.Contains("streamer_name=wowfood", query);
        Assert.Contains("platform=youtube", query);
        Assert.Contains("limit=100", query);
        Assert.Contains("order_by=played_at", query);
        Assert.Contains("order_dir=desc", query);
        Assert.Contains("played_after=2026-08-05T12:00:00.0000000+00:00", query);
    }

    [Fact]
    public async Task Given_NoCredential_When_FetchQueueAsync_Then_FailsBeforeSendingRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = new StreamerSongListApiClient(
            new HttpClient(handler),
            new StubCredentialProvider(null),
            Options());

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Contains("Add an API token in Settings", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_UnauthorizedResponse_When_FetchQueueAsync_Then_ReportsAuthenticationFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"message":"token expired"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("rejected", exception.Message);
        Assert.Contains("token expired", exception.Message);
    }

    [Fact]
    public async Task Given_ValidationErrors_When_FetchPlayHistoryAsync_Then_ReportsFieldDetails()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(
                """
                {
                  "title": "Unprocessable Entity",
                  "status": 422,
                  "detail": "validation failed",
                  "errors": [{
                    "location": "query.limit",
                    "message": "must be less than or equal to 100",
                    "value": 200
                  }]
                }
                """,
                Encoding.UTF8,
                "application/problem+json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchPlayHistoryAsync(
                new StreamerSongListChannel("wowfood"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Contains("validation failed", exception.Message);
        Assert.Contains("query.limit: must be less than or equal to 100", exception.Message);
    }

    [Fact]
    public void Given_PageSizeAboveApiMaximum_When_ConstructingClient_Then_RejectsOptions()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var credential = new StreamerSongListCredential(StreamerSongListCredentialKind.Streamer, "test-token");
        var options = new StreamerSongListApiOptions
        {
            BaseAddress = new Uri("https://example.test/"),
            PageSize = 101
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StreamerSongListApiClient(
            new HttpClient(handler),
            new StubCredentialProvider(credential),
            options));

        Assert.Contains("between 1 and 100", exception.Message);
    }

    [Fact]
    public async Task Given_UnsupportedPlatform_When_FetchQueueAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.FetchQueueAsync(
                new StreamerSongListChannel("wowfood", "unsupported"),
                TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    private static StreamerSongListApiClient CreateClient(
        RecordingHandler handler,
        StreamerSongListCredentialKind kind = StreamerSongListCredentialKind.Streamer,
        string? clientId = null,
        TimeProvider? timeProvider = null)
    {
        var credential = new StreamerSongListCredential(kind, "test-token", clientId);
        return new StreamerSongListApiClient(
            new HttpClient(handler),
            new StubCredentialProvider(credential),
            Options(),
            timeProvider);
    }

    private static StreamerSongListApiOptions Options()
    {
        return new StreamerSongListApiOptions { BaseAddress = new Uri("https://example.test/") };
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubCredentialProvider(StreamerSongListCredential? credential)
        : IStreamerSongListCredentialProvider
    {
        public ValueTask<StreamerSongListCredential?> GetCredentialAsync(
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(credential);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string? ClientId { get; private set; }
        public HttpMethod? Method { get; private set; }
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            ClientId = request.Headers.TryGetValues("Client-Id", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
