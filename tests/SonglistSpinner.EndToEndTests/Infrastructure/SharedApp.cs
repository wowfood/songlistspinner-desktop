using Xunit;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// One app, simulator and profile shared by a test class (<c>IClassFixture&lt;SharedApp&gt;</c>), started by the
/// class's first test and reset before each later one, so most tests skip the app's launch. Nothing starts until
/// a test calls <see cref="BeginTestAsync"/>, so the default test run, where every test skips, opens no window.
/// </summary>
/// <remarks>
/// <see cref="AppScenario.TryResetAsync"/> decides what a reset can undo. When it cannot, or fails, the app is
/// relaunched on a fresh profile against the same (emptied) simulator, so every test starts from a fresh launch's
/// state either way. Each test still seeds the simulator itself.
/// </remarks>
public sealed class SharedApp : IAsyncLifetime
{
    private AppScenario? _scenario;

    /// <summary>
    /// The class's scenario, freshly started for the first test and reset (or relaunched) for each later one.
    /// Call it after <see cref="EndToEnd.SkipUnlessEnabled"/>.
    /// </summary>
    internal async Task<AppScenario> BeginTestAsync(CancellationToken cancellationToken)
    {
        if (_scenario is null)
        {
            _scenario = await AppScenario.StartAsync(cancellationToken);
            return _scenario;
        }

        string? relaunchReason;
        try
        {
            relaunchReason = await _scenario.TryResetAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Whatever the previous test left behind, a fresh profile clears it.
            relaunchReason = $"resetting failed: {ex.Message}";
        }

        if (relaunchReason is null)
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Reused the class's app after resetting it.");
        }
        else
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"Relaunched the app on a fresh profile: {relaunchReason}.");
            await _scenario.RelaunchOnFreshProfileAsync(cancellationToken);
        }

        return _scenario;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_scenario is not null) await _scenario.DisposeAsync();
    }
}
