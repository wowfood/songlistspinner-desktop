using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class SecureStorageStreamerSongListCredentialStoreTests
{
    // Persisted contracts: renaming one loses every user's saved credential.
    private const string TokenKey = "streamersonglist_api_token";
    private const string KindKey = "streamersonglist_api_token_kind";
    private const string ClientIdKey = "streamersonglist_api_client_id";

    private static readonly StreamerSongListCredential EnvironmentCredential =
        new(StreamerSongListCredentialKind.Streamer, "simulator-token");

    private static readonly StreamerSongListCredential OAuthFallback =
        new(StreamerSongListCredentialKind.OAuthBearer, "oauth-token", "client-id");

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Given_NoSavedToken_When_ReadingTheCredential_Then_TheEnvironmentFallbackIsUsed(string? savedToken)
    {
        var secrets = new InMemorySecretStore();
        if (savedToken is not null) secrets.Values[TokenKey] = savedToken;
        var store = CreateStore(secrets, new InMemoryKeyValueStore());

        var credential = await store.GetCredentialAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EnvironmentCredential, credential);
    }

    [Fact]
    public async Task Given_ACredentialWithPadding_When_Saved_Then_ItIsStoredTrimmedUnderItsKeysAndReadBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences);

        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, " saved-token ", " client-id "),
            cancellationToken);
        var credential = await store.GetCredentialAsync(cancellationToken);

        Assert.Equal(new Dictionary<string, string> { [TokenKey] = "saved-token" }, secrets.Values);
        Assert.Equal(
            new Dictionary<string, string> { [KindKey] = "OAuthBearer", [ClientIdKey] = "client-id" },
            preferences.Values);
        Assert.Equal(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "saved-token", "client-id"),
            credential);
    }

    [Fact]
    public async Task Given_ASavedClientId_When_SavingACredentialWithoutOne_Then_TheOldClientIdIsRemoved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var preferences = new InMemoryKeyValueStore { Values = { [ClientIdKey] = "old-client" } };
        var store = CreateStore(new InMemorySecretStore(), preferences);

        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.User, "saved-token", " "),
            cancellationToken);

        Assert.Equal(new Dictionary<string, string> { [KindKey] = "User" }, preferences.Values);
    }

    [Fact]
    public async Task Given_ABlankToken_When_Saving_Then_ItIsRejectedAndNothingIsStored()
    {
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences);

        var failure = await Assert.ThrowsAsync<ArgumentException>(() => store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.Streamer, "  "),
            TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("credential", failure.ParamName);
        Assert.Empty(secrets.Values);
        Assert.Empty(preferences.Values);
    }

    [Fact]
    public async Task Given_NothingSaved_When_SavingTheEnvironmentFallback_Then_NothingIsStored()
    {
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences);

        // What Settings saves when its token field is left blank while only the environment supplies a token.
        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.Streamer, " simulator-token ", " "),
            TestContext.Current.CancellationToken);

        Assert.Empty(secrets.Values);
        Assert.Empty(preferences.Values);
    }

    [Fact]
    public async Task Given_AnotherCredentialSaved_When_SavingTheEnvironmentFallback_Then_TheSavedOneIsRemovedSoTheFallbackApplies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences);
        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "rejected-token", "client-id"),
            cancellationToken);

        // What a failed connection test does when the credential before it was the environment's.
        await store.SaveCredentialAsync(EnvironmentCredential, cancellationToken);
        var credential = await store.GetCredentialAsync(cancellationToken);

        Assert.Empty(secrets.Values);
        Assert.Empty(preferences.Values);
        Assert.Equal(EnvironmentCredential, credential);
    }

    [Fact]
    public async Task Given_AnOAuthFallbackWithAClientId_When_SavingItWithPadding_Then_NothingIsStored()
    {
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences, OAuthFallback);

        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, " oauth-token ", " client-id "),
            TestContext.Current.CancellationToken);

        Assert.Empty(secrets.Values);
        Assert.Empty(preferences.Values);
    }

    [Theory]
    // The user changed something about the credential, so it is theirs now and is saved like any other.
    [InlineData(StreamerSongListCredentialKind.OAuthBearer, "another-client")]
    [InlineData(StreamerSongListCredentialKind.OAuthBearer, null)]
    [InlineData(StreamerSongListCredentialKind.User, "client-id")]
    public async Task Given_AnOAuthFallback_When_SavingItsTokenWithAnotherKindOrClientId_Then_TheEditedCredentialIsStored(
        StreamerSongListCredentialKind kind,
        string? clientId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore(new InMemorySecretStore(), new InMemoryKeyValueStore(), OAuthFallback);
        var edited = new StreamerSongListCredential(kind, "oauth-token", clientId);

        await store.SaveCredentialAsync(edited, cancellationToken);
        var credential = await store.GetCredentialAsync(cancellationToken);

        Assert.Equal(edited, credential);
    }

    [Fact]
    public async Task Given_ASavedCredential_When_Cleared_Then_ItsKeysAreRemovedAndTheEnvironmentFallbackApplies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secrets = new InMemorySecretStore();
        var preferences = new InMemoryKeyValueStore();
        var store = CreateStore(secrets, preferences);
        await store.SaveCredentialAsync(
            new StreamerSongListCredential(StreamerSongListCredentialKind.OAuthBearer, "saved-token", "client-id"),
            cancellationToken);

        await store.ClearCredentialAsync(cancellationToken);
        var credential = await store.GetCredentialAsync(cancellationToken);

        Assert.Empty(secrets.Values);
        Assert.Empty(preferences.Values);
        Assert.Equal(EnvironmentCredential, credential);
    }

    private static SecureStorageStreamerSongListCredentialStore CreateStore(
        InMemorySecretStore secrets,
        InMemoryKeyValueStore preferences,
        StreamerSongListCredential? fallback = null)
    {
        fallback ??= EnvironmentCredential;
        return new SecureStorageStreamerSongListCredentialStore(
            secrets,
            preferences,
            EnvironmentOverrides.Read(
                name => name switch
                {
                    EnvironmentOverrides.AccessTokenVariable => fallback.Token,
                    EnvironmentOverrides.TokenTypeVariable =>
                        fallback.Kind == StreamerSongListCredentialKind.OAuthBearer ? "bearer" : "streamer",
                    EnvironmentOverrides.ClientIdVariable => fallback.ClientId,
                    _ => null
                },
                allowTestProfile: false));
    }
}
