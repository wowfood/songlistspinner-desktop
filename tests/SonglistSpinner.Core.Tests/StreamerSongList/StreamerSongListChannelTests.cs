using SonglistSpinner.Core.StreamerSongList;
using Xunit;

namespace SonglistSpinner.Core.Tests.StreamerSongList;

public class StreamerSongListChannelTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_BlankName_When_Constructed_Then_RejectsIt(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new StreamerSongListChannel(name));

        Assert.Equal("name", exception.ParamName);
        Assert.Equal("A streamer name is required. (Parameter 'name')", exception.Message);
    }

    [Fact]
    public void Given_NameOnly_When_Constructed_Then_UsesTheDefaultPlatform()
    {
        var channel = new StreamerSongListChannel("wowfood");

        Assert.Equal(StreamerSongListPlatformNames.Default, channel.Platform);
    }
}
