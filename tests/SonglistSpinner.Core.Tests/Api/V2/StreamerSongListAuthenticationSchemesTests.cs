using SonglistSpinner.Core.Api.V2;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api.V2;

public class StreamerSongListAuthenticationSchemesTests
{
    [Fact]
    public void Given_AuthenticationSchemes_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(
            ["Bearer", "Streamer", "User"],
            [
                StreamerSongListAuthenticationSchemes.Bearer,
                StreamerSongListAuthenticationSchemes.Streamer,
                StreamerSongListAuthenticationSchemes.User
            ]);
    }
}
