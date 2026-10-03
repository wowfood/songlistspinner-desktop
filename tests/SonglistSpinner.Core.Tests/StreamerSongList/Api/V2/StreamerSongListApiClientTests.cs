using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using Xunit;

namespace SonglistSpinner.Core.Tests.StreamerSongList.Api.V2;

public class StreamerSongListApiClientTests
{
    [Fact]
    public void Given_DefaultOptions_When_ReadingBaseAddress_Then_UsesProductionApiEndpoint()
    {
        var options = new StreamerSongListApiOptions();

        Assert.Equal("https://api.streamersonglist.com/", options.BaseAddress.AbsoluteUri);
    }

    [Fact]
    public async Task Given_Channel_When_ResolveStreamerAsync_Then_UsesStreamerLookupAndReturnsId()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":314}"""));
        var client = CreateClient(handler);

        var streamer = await client.ResolveStreamerAsync(
            new StreamerSongListChannel("Foo Bar", "YOUTUBE"),
            TestContext.Current.CancellationToken);

        Assert.Equal(new StreamerId(314), streamer.Id);
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

        Assert.Equal(new StreamerId(314), streamer.Id);
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
    public async Task Given_InvalidStreamerResponse_When_ResolveStreamerAsync_Then_RejectsId()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":0}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.ResolveStreamerAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Contains("invalid streamer ID", exception.Message);
    }

    [Fact]
    public async Task Given_StreamerCredential_When_FetchQueueSnapshotAsync_Then_UsesV2QueryAndMapsItems()
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

        var result = await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("Foo Bar", "TWITCH"),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
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
    public async Task Given_OAuthCredential_When_FetchQueueSnapshotAsync_Then_AddsBearerAndClientIdHeaders()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":[],"playing":null,"total":0}"""));
        var client = CreateClient(handler, StreamerSongListCredentialKind.OAuthBearer, "desktop-client");

        await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("desktop-client", handler.ClientId);
    }

    [Fact]
    public async Task Given_NullRequestCollection_When_FetchQueueSnapshotAsync_Then_MapsEmptyRequests()
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

        var result = await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(result.Items).Requests);
    }

    [Fact]
    public async Task Given_NullItems_When_FetchQueueSnapshotAsync_Then_ReturnsEmptyQueue()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":null,"playing":null,"total":0}"""));
        var client = CreateClient(handler);

        var result = await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Given_UserCredential_When_FetchQueueSnapshotAsync_Then_UsesUserAuthorizationScheme()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":[],"playing":null,"total":0}"""));
        var client = CreateClient(handler, StreamerSongListCredentialKind.User);

        await client.FetchQueueSnapshotAsync(
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

        await client.MarkQueueItemAsPlayedAsync(new QueueEntryId(91), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/queue/played?queue_id=91", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Streamer", handler.Authorization?.Scheme);
    }

    [Fact]
    public async Task Given_DefaultQueueEntryId_When_MarkQueueItemAsPlayedAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.MarkQueueItemAsPlayedAsync(default, TestContext.Current.CancellationToken));

        Assert.Equal("queueEntryId", exception.ParamName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_StreamerId_When_MarkNowPlayingAsPlayedAsync_Then_PostsPlayingPosition()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.MarkNowPlayingAsPlayedAsync(new StreamerId(314), TestContext.Current.CancellationToken);

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

        await client.PromoteQueueItemToNowPlayingAsync(new QueueEntryId(91), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/queue/91/play", handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Given_DefaultStreamerId_When_MarkNowPlayingAsPlayedAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.MarkNowPlayingAsPlayedAsync(default, TestContext.Current.CancellationToken));

        Assert.Equal("streamerId", exception.ParamName);
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
        var client = CreateClient(handler, timeProvider: new FakeTimeProvider(now));

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

    [Theory]
    [InlineData("day", "2026-03-30T12:00:00.0000000+00:00")]
    // A calendar month, not 30 days: from 31 March that is 28 February, where 30 days would be 1 March.
    [InlineData("month", "2026-02-28T12:00:00.0000000+00:00")]
    [InlineData("all", null)]
    [InlineData("stream", null)]
    public async Task Given_PlayHistoryPeriod_When_FetchPlayHistoryAsync_Then_SendsTheMatchingPlayedAfterFilter(
        string period,
        string? expectedPlayedAfter)
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"items":[],"token":null,"total":0}"""));
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);
        var client = CreateClient(handler, timeProvider: new FakeTimeProvider(now));

        await client.FetchPlayHistoryAsync(
            new StreamerSongListChannel("wowfood"),
            period,
            TestContext.Current.CancellationToken);

        var query = Uri.UnescapeDataString(handler.RequestUri?.Query ?? "");
        if (expectedPlayedAfter is null)
            Assert.DoesNotContain("played_after", query);
        else
            Assert.Contains($"played_after={expectedPlayedAfter}", query);
    }

    [Fact]
    public async Task Given_UnknownPeriod_When_FetchPlayHistoryAsync_Then_RejectsItBeforeSendingRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.FetchPlayHistoryAsync(
                new StreamerSongListChannel("wowfood"),
                "fortnight",
                TestContext.Current.CancellationToken));

        Assert.Equal("period", exception.ParamName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_QueueEntryOffTheSongList_When_FetchQueueSnapshotAsync_Then_UsesTheNonlistTitleAndTheRequestersUsername()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [{
                "id": 93,
                "position": 1,
                "nonlistSong": "Custom Request",
                "requests": [{ "amount": null, "name": "", "user": { "username": "viewer" } }],
                "song": null,
                "songId": null
              }],
              "playing": null,
              "total": 1
            }
            """));
        var client = CreateClient(handler);

        var result = await client.FetchQueueSnapshotAsync(
            new StreamerSongListChannel("wowfood"),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("", item.Song.Artist);
        Assert.Equal("Custom Request", item.Song.Title);
        Assert.Equal("viewer", Assert.Single(item.Requests).Name);
    }

    [Fact]
    public async Task Given_HistoryDonations_When_FetchPlayHistoryAsync_Then_ADonationAttachesOnlyWhereNoRequestHasAnAmount()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "items": [
                {
                  "donationAmount": 5.00,
                  "requests": [],
                  "song": { "artist": "Artist", "title": "Unrequested" },
                  "songId": 8
                },
                {
                  "donationAmount": 3.00,
                  "requests": [{ "amount": 2.00, "name": "tipper", "user": null }],
                  "song": { "artist": "Artist", "title": "Tipped Request" },
                  "songId": 9
                }
              ],
              "token": null,
              "total": 2
            }
            """));
        var client = CreateClient(handler);

        var result = await client.FetchPlayHistoryAsync(
            new StreamerSongListChannel("wowfood"),
            cancellationToken: TestContext.Current.CancellationToken);

        var unrequested = Assert.Single(result[0].Requests);
        Assert.Equal(5.00m, unrequested.DonationAmount);
        Assert.Equal("", unrequested.Name);
        var tipped = Assert.Single(result[1].Requests);
        Assert.Equal(2.00m, tipped.Amount);
        Assert.Null(tipped.DonationAmount);
    }

    [Fact]
    public async Task Given_ForbiddenResponse_When_FetchQueueSnapshotAsync_Then_ReportsTheChannelIsNotAccessible()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"detail":"channel is private"}""", Encoding.UTF8, "application/problem+json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(
            "The configured StreamerSongList token cannot access this channel. channel is private",
            exception.Message);
    }

    [Fact]
    public async Task Given_ErrorResponseWithAnHtmlBody_When_FetchQueueSnapshotAsync_Then_ReportsTheStatusWithoutTheBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html><body>Bad Gateway</body></html>", Encoding.UTF8, "text/html")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Equal("StreamerSongList returned HTTP 502 (Bad Gateway).", exception.Message);
    }

    [Fact]
    public async Task Given_SuccessResponseThatIsNotApiJson_When_FetchQueueSnapshotAsync_Then_ReportsAnApiMismatch()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Captive portal</body></html>", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Equal("StreamerSongList returned a response that does not match API v2.", exception.Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task Given_NoCredential_When_FetchQueueSnapshotAsync_Then_FailsBeforeSendingRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = new StreamerSongListApiClient(
            new HttpClient(handler),
            new StubCredentialProvider(null),
            Options());

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Contains("Add an API token in Settings", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_UndefinedCredentialKind_When_FetchQueueSnapshotAsync_Then_FailsNamingTheKindBeforeSendingRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler, (StreamerSongListCredentialKind)99);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood"),
                TestContext.Current.CancellationToken));

        Assert.Equal("Unsupported StreamerSongList credential kind '99'.", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_UnauthorizedResponse_When_FetchQueueSnapshotAsync_Then_ReportsAuthenticationFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"message":"token expired"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<StreamerSongListApiException>(() =>
            client.FetchQueueSnapshotAsync(
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

        Assert.Equal("options", exception.ParamName);
        Assert.Equal("Page size must be between 1 and 100. (Parameter 'options')", exception.Message);
    }

    [Fact]
    public async Task Given_UnsupportedPlatform_When_FetchQueueSnapshotAsync_Then_RejectsRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.FetchQueueSnapshotAsync(
                new StreamerSongListChannel("wowfood", "unsupported"),
                TestContext.Current.CancellationToken));

        Assert.Equal("channel", exception.ParamName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Given_RequestWithQueryAndToken_When_Sent_Then_LogsOnlyMethodPathAndStatus()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":314}"""));
        var logger = new RecordingLogger();
        var client = CreateClient(handler, logger: logger);

        await client.ResolveStreamerAsync(
            new StreamerSongListChannel("Foo Bar", "twitch"),
            TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("StreamerSongList GET /streamers returned 200", entry.Message);
    }

    private static StreamerSongListApiClient CreateClient(
        RecordingHandler handler,
        StreamerSongListCredentialKind kind = StreamerSongListCredentialKind.Streamer,
        string? clientId = null,
        TimeProvider? timeProvider = null,
        ILogger<StreamerSongListApiClient>? logger = null)
    {
        var credential = new StreamerSongListCredential(kind, "test-token", clientId);
        return new StreamerSongListApiClient(
            new HttpClient(handler),
            new StubCredentialProvider(credential),
            Options(),
            timeProvider,
            logger);
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

    private sealed class RecordingLogger : ILogger<StreamerSongListApiClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
