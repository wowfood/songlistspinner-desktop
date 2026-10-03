using SonglistSpinner.Core.Api.V2;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api.V2;

public class CentrifugoStreamerSongListEventSourceTests
{
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
}
