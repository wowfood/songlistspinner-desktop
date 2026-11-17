using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

/// <remarks>The server itself is tested over real HTTP in SonglistSpinner.IntegrationTests.</remarks>
public class LocalOverlayServerTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    // How Uri.Host spells the IPv6 loopback address.
    [InlineData("[::1]")]
    public void Given_ARequestForThisMachine_When_CheckingItsHost_Then_ItIsAllowed(string host)
    {
        Assert.True(LocalOverlayServer.IsAllowedHost(host));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("attacker.example")]
    [InlineData("localhost.attacker.example")]
    [InlineData("192.168.1.20")]
    public void Given_ARequestNamingAnotherHost_When_CheckingItsHost_Then_ItIsRejected(string? host)
    {
        Assert.False(LocalOverlayServer.IsAllowedHost(host));
    }
}
