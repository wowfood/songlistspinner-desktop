using SonglistSpinner.Core.Services;
using Xunit;

namespace SonglistSpinner.Core.Tests.Services;

public class StreamerSongListReferenceParserTests
{
    [Theory]
    [InlineData("https://streamersonglist.com/t/wowfood", "none", "wowfood", "twitch")]
    [InlineData("https://streamersonglist.com/s/wowfood", "twitch", "wowfood", "none")]
    [InlineData("/k/kick_name", "twitch", "kick_name", "kick")]
    [InlineData("y/youtube-name", "twitch", "youtube-name", "youtube")]
    [InlineData("@plain_name", "kick", "plain_name", "kick")]
    public void Given_ValidReference_When_Parsing_Then_ReturnsChannel(
        string reference,
        string fallbackPlatform,
        string expectedName,
        string expectedPlatform)
    {
        var parsed = StreamerSongListReferenceParser.TryParse(
            reference,
            fallbackPlatform,
            out var channel,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(channel);
        Assert.Equal(expectedName, channel.Name);
        Assert.Equal(expectedPlatform, channel.Platform);
    }

    [Theory]
    [InlineData("", "Enter a StreamerSongList URL or streamer name.")]
    [InlineData(
        "https://streamersonglist.com/not-a-channel",
        "Use a streamer name or a URL ending in /t/name, /s/name, /k/name, or /y/name.")]
    [InlineData("ftp://streamersonglist.com/t/wowfood", "The streamer URL must use http or https.")]
    public void Given_InvalidReference_When_Parsing_Then_ReturnsHelpfulError(string reference, string expectedError)
    {
        var parsed = StreamerSongListReferenceParser.TryParse(
            reference,
            "twitch",
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Equal(expectedError, error);
    }
}
