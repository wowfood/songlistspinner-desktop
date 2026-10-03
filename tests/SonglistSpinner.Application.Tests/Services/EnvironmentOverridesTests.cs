using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Core.Updates;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class EnvironmentOverridesTests
{
    [Fact]
    public void Given_SimulatorAddresses_When_Reading_Then_TheApiAndEventsUseThem()
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.ApiBaseUrlVariable] = "http://127.0.0.1:5199/",
            [EnvironmentOverrides.EventsUrlVariable] = "ws://127.0.0.1:5199/connection/websocket"
        });

        Assert.Equal(new Uri("http://127.0.0.1:5199/"), overrides.Api.BaseAddress);
        Assert.Equal(new Uri("ws://127.0.0.1:5199/connection/websocket"), overrides.Events.Endpoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("relative/path")]
    public void Given_AMissingOrInvalidAddress_When_Reading_Then_TheProductionEndpointsAreKept(string? address)
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.ApiBaseUrlVariable] = address,
            [EnvironmentOverrides.EventsUrlVariable] = address
        });

        Assert.Equal(StreamerSongListApiOptions.ProductionBaseAddress, overrides.Api.BaseAddress);
        Assert.Equal(StreamerSongListEventsOptions.ProductionEndpoint, overrides.Events.Endpoint);
    }

    [Fact]
    public void Given_ATokenWithTypeAndClientId_When_Reading_Then_TheyFormTheFallbackCredential()
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.AccessTokenVariable] = "simulator-token",
            [EnvironmentOverrides.TokenTypeVariable] = "bearer",
            [EnvironmentOverrides.ClientIdVariable] = "desktop-client"
        });

        Assert.Equal(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "simulator-token", "desktop-client"),
            overrides.FallbackCredential);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Given_NoToken_When_Reading_Then_ThereIsNoFallbackCredential(string? token)
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.AccessTokenVariable] = token,
            [EnvironmentOverrides.TokenTypeVariable] = "user"
        });

        Assert.Null(overrides.FallbackCredential);
    }

    [Theory]
    [InlineData("streamer", StreamerSongListCredentialKind.Streamer)]
    [InlineData("USER", StreamerSongListCredentialKind.User)]
    [InlineData("OAuthBearer", StreamerSongListCredentialKind.OAuthBearer)]
    [InlineData(" oauth ", StreamerSongListCredentialKind.OAuthBearer)]
    [InlineData("Bearer", StreamerSongListCredentialKind.OAuthBearer)]
    [InlineData(null, StreamerSongListCredentialKind.Streamer)]
    [InlineData("unrecognised", StreamerSongListCredentialKind.Streamer)]
    public void Given_ATokenType_When_Reading_Then_TheCredentialKindFollowsItsNameOrAlias(
        string? tokenType,
        StreamerSongListCredentialKind expectedKind)
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.AccessTokenVariable] = "simulator-token",
            [EnvironmentOverrides.TokenTypeVariable] = tokenType
        });

        Assert.Equal(expectedKind, overrides.FallbackCredential?.Kind);
    }

    [Fact]
    public void Given_NoTestVariables_When_Reading_Then_TheUserProfileOverlayPortAndGitHubAreKept()
    {
        var overrides = Read([]);

        Assert.Null(overrides.ProfileDirectory);
        Assert.Equal(LocalOverlayServer.DefaultPort, overrides.OverlayPort);
        Assert.Equal(GitHubReleaseUpdateChecker.LatestReleaseEndpoint, overrides.UpdateReleaseEndpoint);
    }

    [Fact]
    public void Given_AnAbsoluteProfileDirectory_When_Reading_Then_ItIsTheProfile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "songlistspinner-e2e");

        var overrides = Read(new() { [EnvironmentOverrides.ProfileDirectoryVariable] = directory });

        Assert.Equal(directory, overrides.ProfileDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("profile")]
    [InlineData(@"..\profile")]
    public void Given_ARelativeProfileDirectory_When_Reading_Then_TheUserProfileIsKept(string directory)
    {
        var overrides = Read(new() { [EnvironmentOverrides.ProfileDirectoryVariable] = directory });

        Assert.Null(overrides.ProfileDirectory);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("51999", 51999)]
    [InlineData("65535", 65535)]
    [InlineData("0", LocalOverlayServer.DefaultPort)]
    [InlineData("65536", LocalOverlayServer.DefaultPort)]
    [InlineData("-1", LocalOverlayServer.DefaultPort)]
    [InlineData("port", LocalOverlayServer.DefaultPort)]
    public void Given_AnOverlayPort_When_Reading_Then_OnlyAValidPortReplacesTheDefault(string port, int expectedPort)
    {
        var overrides = Read(new() { [EnvironmentOverrides.OverlayPortVariable] = port });

        Assert.Equal(expectedPort, overrides.OverlayPort);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5199/_simulator/releases/latest", "http://127.0.0.1:5199/_simulator/releases/latest")]
    [InlineData("not a url", null)]
    public void Given_AnUpdateReleaseUrl_When_Reading_Then_OnlyAnAbsoluteUrlReplacesGitHub(
        string url,
        string? expectedEndpoint)
    {
        var overrides = Read(new() { [EnvironmentOverrides.UpdateReleaseUrlVariable] = url });

        Assert.Equal(
            expectedEndpoint is null ? GitHubReleaseUpdateChecker.LatestReleaseEndpoint : new Uri(expectedEndpoint),
            overrides.UpdateReleaseEndpoint);
    }

    [Theory]
    [InlineData(true, "--remote-debugging-port=9222", "--remote-debugging-port=9222")]
    [InlineData(false, "--remote-debugging-port=9222", null)]
    [InlineData(true, " ", null)]
    public void Given_WebViewBrowserArguments_When_Reading_Then_OnlyATestProfileForwardsThem(
        bool testProfile,
        string arguments,
        string? expectedArguments)
    {
        var overrides = Read(new()
        {
            [EnvironmentOverrides.ProfileDirectoryVariable] =
                testProfile ? Path.Combine(Path.GetTempPath(), "songlistspinner-e2e") : null,
            [EnvironmentOverrides.WebViewBrowserArgumentsVariable] = arguments
        });

        Assert.Equal(expectedArguments, overrides.WebViewBrowserArguments);
    }

    private static EnvironmentOverrides Read(Dictionary<string, string?> variables) =>
        EnvironmentOverrides.Read(name => variables.GetValueOrDefault(name));
}
