using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.Contracts;

namespace SonglistSpinner.Services;

/// <summary>
/// Tests the StreamerSongList connection with a candidate API credential and puts the previous credential
/// back when the test fails, so a failed test never leaves an untested credential in secure storage.
/// Settings and Setup both save a credential this way.
/// </summary>
public sealed class ApiCredentialTest
{
    private readonly IStreamerSongListCredentialStore _credentialStore;
    private readonly ILogger<ApiCredentialTest> _logger;

    public ApiCredentialTest(
        IStreamerSongListCredentialStore credentialStore,
        ILogger<ApiCredentialTest>? logger = null)
    {
        _credentialStore = credentialStore;
        _logger = logger ?? NullLogger<ApiCredentialTest>.Instance;
    }

    /// <summary>
    /// Runs <paramref name="testConnection"/>, which uses <paramref name="candidate"/>. The caller stores the
    /// candidate before the test or as its first step. When the test fails and the candidate differs from
    /// <paramref name="previous"/>, the previous credential is stored again, or the stored one cleared when
    /// there was none. Cancellation through <paramref name="cancellationToken"/> propagates without a restore,
    /// because the caller has gone.
    /// </summary>
    /// <returns>What the test found, such as the channel it reached.</returns>
    /// <exception cref="ApiCredentialTestFailedException">The test failed; it reports what happened to the previous credential.</exception>
    public async Task<T> RunAsync<T>(
        StreamerSongListCredential? previous,
        StreamerSongListCredential? candidate,
        Func<CancellationToken, Task<T>> testConnection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await testConnection(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (Equals(previous, candidate))
                throw new ApiCredentialTestFailedException(ex, previousRestored: false, restoreFailure: null);

            try
            {
                if (previous is null)
                    await _credentialStore.ClearCredentialAsync(cancellationToken);
                else
                    await _credentialStore.SaveCredentialAsync(previous, cancellationToken);
            }
            catch (Exception restoreFailure)
            {
                _logger.LogError(restoreFailure, "Restoring the previous API credential failed");
                throw new ApiCredentialTestFailedException(ex, previousRestored: false, restoreFailure);
            }

            throw new ApiCredentialTestFailedException(ex, previousRestored: true, restoreFailure: null);
        }
    }
}
