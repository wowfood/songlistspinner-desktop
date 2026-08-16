using SonglistSpinner.Core.Api.V2;
using SonglistSpinner.Core.Contracts;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api;

public class CentrifugoProtocolTests
{
    [Theory]
    [InlineData("now_playing_update")]
    [InlineData("queue_add")]
    [InlineData("queue_clear")]
    [InlineData("queue_remove")]
    [InlineData("queue_reorder")]
    [InlineData("queue_update")]
    public void Given_QueuePublication_When_ParsingNotification_Then_ReturnsQueueChanged(string eventType)
    {
        var message = Publication(eventType, """{"id":91}""");

        var parsed = CentrifugoProtocol.TryParseNotification(message, out var notification);

        Assert.True(parsed);
        Assert.Equal(StreamerSongListEventKind.QueueChanged, notification?.Kind);
        Assert.Equal(eventType, notification?.EventType);
    }

    [Theory]
    [InlineData("play_history_add")]
    [InlineData("play_history_remove")]
    public void Given_PlayHistoryPublication_When_ParsingNotification_Then_ReturnsPlayHistoryChanged(string eventType)
    {
        var message = Publication(eventType, "null");

        var parsed = CentrifugoProtocol.TryParseNotification(message, out var notification);

        Assert.True(parsed);
        Assert.Equal(StreamerSongListEventKind.PlayHistoryChanged, notification?.Kind);
        Assert.Equal(eventType, notification?.EventType);
    }

    [Fact]
    public void Given_UnrelatedPublication_When_ParsingNotification_Then_IgnoresIt()
    {
        var message = Publication("song_update", "null");

        var parsed = CentrifugoProtocol.TryParseNotification(message, out var notification);

        Assert.False(parsed);
        Assert.Null(notification);
    }

    [Fact]
    public void Given_CommandError_When_ParsingReply_Then_ReturnsMessage()
    {
        const string message = """{"id":2,"error":{"code":103,"message":"permission denied"}}""";

        var isReply = CentrifugoProtocol.IsCommandReply(message, 2, out var error);

        Assert.True(isReply);
        Assert.Equal("permission denied", error);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"2\"}")]
    [InlineData("{\"id\":2.5}")]
    public void Given_MalformedCommandReply_When_ParsingReply_Then_IgnoresIt(string message)
    {
        var isReply = CentrifugoProtocol.IsCommandReply(message, 2, out var error);

        Assert.False(isReply);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"push\":null}")]
    [InlineData("{\"push\":{\"pub\":null}}")]
    [InlineData("{\"push\":{\"pub\":{\"data\":{\"type\":42}}}}")]
    public void Given_MalformedPublication_When_ParsingNotification_Then_IgnoresIt(string message)
    {
        var parsed = CentrifugoProtocol.TryParseNotification(message, out var notification);

        Assert.False(parsed);
        Assert.Null(notification);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"push\":null}")]
    public void Given_MalformedFrame_When_CheckingDisconnect_Then_IgnoresIt(string message)
    {
        Assert.False(CentrifugoProtocol.IsDisconnect(message));
    }

    [Fact]
    public void Given_EmptyObject_When_CheckingApplicationPing_Then_ReturnsTrue()
    {
        Assert.True(CentrifugoProtocol.IsApplicationPing("  {}\r\n"));
    }

    [Fact]
    public void Given_DefaultOptions_When_ConstructingEventSource_Then_UsesProductionWebSocketEndpoint()
    {
        var options = new StreamerSongListEventsOptions();

        _ = new CentrifugoStreamerSongListEventSource(options);

        Assert.Equal(
            "wss://events.streamersonglist.com/connection/websocket",
            options.Endpoint.AbsoluteUri);
    }

    [Fact]
    public void Given_HttpEndpoint_When_ConstructingEventSource_Then_RejectsOptions()
    {
        var options = new StreamerSongListEventsOptions
        {
            Endpoint = new Uri("https://events.example.test/connection/websocket")
        };

        Assert.Throws<ArgumentException>(() => new CentrifugoStreamerSongListEventSource(options));
    }

    [Fact]
    public void Given_NonPositiveReceiveTimeout_When_ConstructingEventSource_Then_RejectsOptions()
    {
        var options = new StreamerSongListEventsOptions { ReceiveIdleTimeout = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => new CentrifugoStreamerSongListEventSource(options));
    }

    private static string Publication(string eventType, string data)
    {
        return $$"""
                 {
                   "push": {
                     "channel": "streamer:314-queue",
                     "pub": {
                       "data": {
                         "type": "{{eventType}}",
                         "data": {{data}}
                       }
                     }
                   }
                 }
                 """;
    }
}
