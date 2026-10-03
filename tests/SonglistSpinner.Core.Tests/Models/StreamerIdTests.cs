using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class StreamerIdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_NonPositiveValue_When_Constructed_Then_RejectsIt(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StreamerId(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Given_StreamerId_When_Formatted_Then_WritesTheNumberOnly()
    {
        var streamerId = new StreamerId(314);

        var text = $"streamer:{streamerId}-queue";

        Assert.Equal("streamer:314-queue", text);
    }
}
