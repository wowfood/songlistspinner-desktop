using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class CredentialDraftTests
{
    private static readonly StreamerSongListCredential Saved =
        new(StreamerSongListCredentialKind.Streamer, "saved-token");

    [Fact]
    public void Given_ASavedCredential_When_ANewTokenIsEntered_Then_TheNewTokenIsSavedWithTheEditedFields()
    {
        var credential = CredentialDraft.ToCredential(
            StreamerSongListCredentialKind.OAuthBearer,
            "  new-token  ",
            " client ",
            Saved);

        Assert.Equal(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "new-token", "client"),
            credential);
    }

    [Fact]
    public void Given_ASavedCredential_When_TheTokenIsBlankAndTheKindAndClientIdAreEdited_Then_TheSavedTokenIsKeptWithTheEditedFields()
    {
        var credential = CredentialDraft.ToCredential(
            StreamerSongListCredentialKind.OAuthBearer,
            "   ",
            "client",
            Saved);

        Assert.Equal(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "saved-token", "client"),
            credential);
    }

    [Fact]
    public void Given_NoSavedCredential_When_TheTokenIsBlank_Then_ThereIsNoCredentialToSave()
    {
        var credential = CredentialDraft.ToCredential(
            StreamerSongListCredentialKind.OAuthBearer,
            "",
            "client",
            saved: null);

        Assert.Null(credential);
    }

    [Fact]
    public void Given_AWhitespaceClientId_When_ATokenIsEntered_Then_TheCredentialHasNoClientId()
    {
        var credential = CredentialDraft.ToCredential(
            StreamerSongListCredentialKind.User,
            "new-token",
            "   ",
            saved: null);

        Assert.Equal(
            new StreamerSongListCredential(StreamerSongListCredentialKind.User, "new-token", ClientId: null),
            credential);
    }
}
