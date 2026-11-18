using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class OverlayServerSentEventsTests
{
    [Fact]
    public void Given_NamedEvent_When_Framing_Then_ItIsAnSseEventWithItsJsonAsData()
    {
        var overlayEvent = OverlayEvent.Named(OverlayEventNames.SetCollapse, """{"collapsed":true}""");

        var frame = OverlayServerSentEvents.Frame(overlayEvent);

        Assert.Equal("event: set_collapse\ndata: {\"collapsed\":true}\n\n", frame);
    }

    [Fact]
    public void Given_KeepAlive_When_Framing_Then_ItIsAnSseComment()
    {
        var frame = OverlayServerSentEvents.Frame(OverlayEvent.KeepAlive);

        Assert.Equal(": keep-alive\n\n", frame);
    }
}
