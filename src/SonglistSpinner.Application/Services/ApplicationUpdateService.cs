using SonglistSpinner.Core.Updates;

namespace SonglistSpinner.Services;

/// <summary>
/// The newer release to offer the user, at most one check per app run, minus the release the user dismissed
/// (remembered across runs under a persisted key).
/// </summary>
public sealed class ApplicationUpdateService
{
    private const string DismissedReleaseKey = "dismissed_application_update";
    private readonly object _checkGate = new();
    private readonly GitHubReleaseUpdateChecker _checker;
    private readonly IKeyValueStore _preferences;
    private readonly Version _currentVersion;
    private Task<ApplicationUpdateInfo?>? _checkTask;

    /// <param name="currentVersion">
    /// The running app's version: the Desktop assembly's, which carries the release's <c>VersionPrefix</c>, not
    /// this assembly's.
    /// </param>
    public ApplicationUpdateService(
        GitHubReleaseUpdateChecker checker,
        IKeyValueStore preferences,
        Version currentVersion)
    {
        _checker = checker;
        _preferences = preferences;
        _currentVersion = new Version(
            Math.Max(0, currentVersion.Major),
            Math.Max(0, currentVersion.Minor),
            Math.Max(0, currentVersion.Build));
    }

    public string CurrentVersion => _currentVersion.ToString(3);

    public async Task<ApplicationUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        Task<ApplicationUpdateInfo?> checkTask;
        lock (_checkGate)
            checkTask = _checkTask ??= CheckCoreAsync(CancellationToken.None);

        try
        {
            return await checkTask.WaitAsync(cancellationToken);
        }
        catch when (checkTask.IsFaulted)
        {
            lock (_checkGate)
            {
                if (ReferenceEquals(_checkTask, checkTask)) _checkTask = null;
            }
            throw;
        }
    }

    public void Dismiss(ApplicationUpdateInfo update)
    {
        _preferences.SetValue(DismissedReleaseKey, update.Tag);
    }

    private async Task<ApplicationUpdateInfo?> CheckCoreAsync(CancellationToken cancellationToken)
    {
        var update = await _checker.CheckAsync(_currentVersion, cancellationToken);
        if (update is null) return null;

        var dismissedTag = _preferences.GetValue(DismissedReleaseKey);
        return string.Equals(dismissedTag, update.Tag, StringComparison.OrdinalIgnoreCase) ? null : update;
    }
}
