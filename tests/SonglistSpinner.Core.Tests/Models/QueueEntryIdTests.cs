using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class QueueEntryIdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_NonPositiveValue_When_Constructed_Then_RejectsIt(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new QueueEntryId(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Given_QueueEntryId_When_Formatted_Then_WritesTheNumberOnly()
    {
        var queueEntryId = new QueueEntryId(91);

        var text = $"queue/{queueEntryId}/play";

        Assert.Equal("queue/91/play", text);
    }
}
