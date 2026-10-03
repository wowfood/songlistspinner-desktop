using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
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

    private static EnvironmentOverrides Read(Dictionary<string, string?> variables) =>
        EnvironmentOverrides.Read(name => variables.GetValueOrDefault(name));
}
