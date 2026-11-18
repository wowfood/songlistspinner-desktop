namespace SonglistSpinner.Services;

/// <summary>
/// An <see cref="ApiCredentialTest"/> failed. <see cref="Exception.InnerException"/> is why the connection
/// test failed; the other members say what became of the credential stored before the test.
/// </summary>
public sealed class ApiCredentialTestFailedException : Exception
{
    public ApiCredentialTestFailedException(Exception testFailure, bool previousRestored, Exception? restoreFailure)
        : base(testFailure.Message, testFailure)
    {
        PreviousRestored = previousRestored;
        RestoreFailure = restoreFailure;
    }

    /// <summary>True when the test replaced a credential and the previous one was stored again.</summary>
    public bool PreviousRestored { get; }

    /// <summary>Why the previous credential could not be stored again, or null when no restore failed.</summary>
    public Exception? RestoreFailure { get; }

    /// <summary>The sentence the pages show after the test failure when the restore failed.</summary>
    public string? RestoreFailureDescription => RestoreFailure is null
        ? null
        : $"The previous credential could not be restored: {RestoreFailure.Message}";
}
