using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class ApiCredentialTestTests
{
    private static readonly StreamerSongListCredential Previous =
        new(StreamerSongListCredentialKind.Streamer, "previous-token");

    private static readonly StreamerSongListCredential Candidate =
        new(StreamerSongListCredentialKind.OAuthBearer, "candidate-token", "client");

    [Fact]
    public async Task Given_CandidateWasStored_When_TheConnectionTestPasses_Then_CandidateStaysStored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore(Previous);
        var credentialTest = new ApiCredentialTest(store);

        await credentialTest.RunAsync(Previous, Candidate, StoreCandidate(store), cancellationToken);

        Assert.Equal(Candidate, store.Credential);
    }

    [Fact]
    public async Task Given_CandidateWasStored_When_TheConnectionTestFails_Then_PreviousCredentialIsRestored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore(Previous);
        var credentialTest = new ApiCredentialTest(store);
        var testFailure = new HttpRequestException("Unauthorized");

        var failure = await Assert.ThrowsAsync<ApiCredentialTestFailedException>(() => credentialTest.RunAsync(
            Previous,
            Candidate,
            StoreCandidate(store, thenFailWith: testFailure),
            cancellationToken));

        Assert.Equal(Previous, store.Credential);
        Assert.True(failure.PreviousRestored);
        Assert.Same(testFailure, failure.InnerException);
        Assert.Null(failure.RestoreFailureDescription);
    }

    [Fact]
    public async Task Given_NoCredentialBeforeTheTest_When_TheConnectionTestFails_Then_StoredCredentialIsCleared()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore(null);
        var credentialTest = new ApiCredentialTest(store);

        var failure = await Assert.ThrowsAsync<ApiCredentialTestFailedException>(() => credentialTest.RunAsync(
            null,
            Candidate,
            StoreCandidate(store, thenFailWith: new HttpRequestException("Unauthorized")),
            cancellationToken));

        Assert.Null(store.Credential);
        Assert.True(failure.PreviousRestored);
    }

    [Fact]
    public async Task Given_CandidateIsThePreviousCredential_When_TheConnectionTestFails_Then_NothingIsRestored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore(Previous);
        var credentialTest = new ApiCredentialTest(store);

        var failure = await Assert.ThrowsAsync<ApiCredentialTestFailedException>(() => credentialTest.RunAsync(
            Previous,
            Previous,
            Task<bool> (_) => throw new HttpRequestException("Unauthorized"),
            cancellationToken));

        Assert.False(failure.PreviousRestored);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Given_RestoringThePreviousCredentialFails_When_TheConnectionTestFails_Then_FailureDescribesTheRestoreFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore(Previous);
        var credentialTest = new ApiCredentialTest(store);

        var failure = await Assert.ThrowsAsync<ApiCredentialTestFailedException>(() => credentialTest.RunAsync(
            Previous,
            Candidate,
            async Task<bool> (cancellation) =>
            {
                await store.SaveCredentialAsync(Candidate, cancellation);
                store.SaveFailure = new IOException("Secure storage is locked");
                throw new HttpRequestException("Unauthorized");
            },
            cancellationToken));

        Assert.False(failure.PreviousRestored);
        Assert.Equal("Unauthorized", failure.Message);
        Assert.Equal(
            "The previous credential could not be restored: Secure storage is locked",
            failure.RestoreFailureDescription);
    }

    [Fact]
    public async Task Given_CallerCancels_When_TheConnectionTestIsCancelled_Then_CancellationPropagatesWithoutARestore()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new InMemoryCredentialStore(Previous);
        var credentialTest = new ApiCredentialTest(store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => credentialTest.RunAsync(
            Previous,
            Candidate,
            async token =>
            {
                await store.SaveCredentialAsync(Candidate, token);
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return true;
            },
            cancellation.Token));

        Assert.Equal(Candidate, store.Credential);
    }

    private static Func<CancellationToken, Task<bool>> StoreCandidate(
        InMemoryCredentialStore store,
        Exception? thenFailWith = null)
    {
        return async cancellationToken =>
        {
            await store.SaveCredentialAsync(Candidate, cancellationToken);
            if (thenFailWith is not null) throw thenFailWith;
            return true;
        };
    }

    private sealed class InMemoryCredentialStore(StreamerSongListCredential? credential)
        : IStreamerSongListCredentialStore
    {
        public StreamerSongListCredential? Credential { get; private set; } = credential;

        public int Writes { get; private set; }

        public Exception? SaveFailure { get; set; }

        public ValueTask<StreamerSongListCredential?> GetCredentialAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Credential);

        public ValueTask SaveCredentialAsync(
            StreamerSongListCredential credential,
            CancellationToken cancellationToken = default)
        {
            if (SaveFailure is not null) throw SaveFailure;
            Writes++;
            Credential = credential;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearCredentialAsync(CancellationToken cancellationToken = default)
        {
            Writes++;
            Credential = null;
            return ValueTask.CompletedTask;
        }
    }
}
