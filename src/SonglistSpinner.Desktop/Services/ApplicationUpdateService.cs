using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

public sealed class ApplicationUpdateService
{
    private const string DismissedReleaseKey = "dismissed_application_update";
    private readonly GitHubReleaseUpdateChecker _checker;
    private readonly Version _currentVersion;
    private Task<ApplicationUpdateInfo?>? _checkTask;

    public ApplicationUpdateService(GitHubReleaseUpdateChecker checker)
    {
        _checker = checker;
        var assemblyVersion = typeof(ApplicationUpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
        _currentVersion = new Version(
            Math.Max(0, assemblyVersion.Major),
            Math.Max(0, assemblyVersion.Minor),
            Math.Max(0, assemblyVersion.Build));
    }

    public string CurrentVersion => _currentVersion.ToString(3);

    public async Task<ApplicationUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        _checkTask ??= CheckCoreAsync(CancellationToken.None);
        return await _checkTask.WaitAsync(cancellationToken);
    }

    public void Dismiss(ApplicationUpdateInfo update)
    {
        Preferences.Set(DismissedReleaseKey, update.Tag);
    }

    private async Task<ApplicationUpdateInfo?> CheckCoreAsync(CancellationToken cancellationToken)
    {
        var update = await _checker.CheckAsync(_currentVersion, cancellationToken);
        if (update is null) return null;

        var dismissedTag = Preferences.Get(DismissedReleaseKey, "");
        return string.Equals(dismissedTag, update.Tag, StringComparison.OrdinalIgnoreCase) ? null : update;
    }
}
