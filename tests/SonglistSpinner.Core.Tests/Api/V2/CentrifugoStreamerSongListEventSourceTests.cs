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

        var exception = Assert.Throws<ArgumentException>(() => new CentrifugoStreamerSongListEventSource(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Equal(
            "The StreamerSongList event endpoint must be an absolute WebSocket URI. (Parameter 'options')",
            exception.Message);
    }

    [Fact]
    public void Given_NonPositiveReceiveTimeout_When_ConstructingEventSource_Then_RejectsOptions()
    {
        var options = new StreamerSongListEventsOptions { ReceiveIdleTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CentrifugoStreamerSongListEventSource(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Equal("The receive idle timeout must be positive. (Parameter 'options')", exception.Message);
    }
}
