using SonglistSpinner.Core.StreamerSongList;
using Xunit;

namespace SonglistSpinner.Core.Tests.StreamerSongList;

public class StreamerSongListPlatformNamesTests
{
    [Fact]
    public void Given_PlatformNames_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(["twitch", "youtube", "kick", "none"], StreamerSongListPlatformNames.Values);
    }

    [Fact]
    public void Given_PlatformNames_When_ComparingValues_Then_EachValueIsCaseInsensitivelyUnique()
    {
        Assert.Distinct(StreamerSongListPlatformNames.Values, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_MixedCasePlatform_When_Normalizing_Then_ReturnsCanonicalValue()
    {
        var success = StreamerSongListPlatformNames.TryNormalize(" YouTube ", out var result);

        Assert.True(success);
        Assert.Equal("youtube", result);
    }
}
