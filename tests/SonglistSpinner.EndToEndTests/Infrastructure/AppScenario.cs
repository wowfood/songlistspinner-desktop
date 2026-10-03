using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.Simulator;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// A test's world: an empty simulator, a test profile and the app running on both, plus the page objects and an
/// OBS overlay browser opened on demand. A test seeds the simulator itself (see <c>ChannelSeed</c>).
/// <see cref="SharedApp"/> keeps one for a whole test class and resets it between tests; a test that needs a
/// first run or a restart starts its own with <see cref="StartAsync"/>. Disposing kills the app, deletes the
/// profile, closes the overlay browser and stops the simulator, whatever the test's outcome.
/// </summary>
internal sealed class AppScenario : IAsyncDisposable
{
    private readonly bool _useEnvironmentCredential;
    private OverlayBrowser? _overlayBrowser;
    private string _savedStateAtLaunch = "";

    private AppScenario(StreamerSongListSimulator simulator, TestProfile profile, bool useEnvironmentCredential)
    {
        Simulator = simulator;
        Profile = profile;
        _useEnvironmentCredential = useEnvironmentCredential;
    }

    public StreamerSongListSimulator Simulator { get; }

    public TestProfile Profile { get; private set; }

    public DesktopApp App { get; private set; } = null!;

    public IPage Page => App.Page;

    public DashboardPage Dashboard => new(Page);

    public SettingsPage Settings => new(Page);

    public SetupWizard Setup => new(Page);

    public UpdateBanner UpdateBanner => new(Page);

    /// <summary>
    /// Starts an empty simulator and the app on a fresh profile. <paramref name="prepare"/> runs before the app
    /// starts: seed the simulator there when the app must find data at startup (a default channel, a release),
    /// or save settings into the profile.
    /// </summary>
    /// <param name="useEnvironmentCredential">
    /// False starts the app with no credential at all, so it opens the Setup wizard as on a first run.
    /// </param>
    public static async Task<AppScenario> StartAsync(
        CancellationToken cancellationToken,
        Func<StreamerSongListSimulator, TestProfile, Task>? prepare = null,
        bool useEnvironmentCredential = true)
    {
        var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        TestProfile? profile = null;
        try
        {
            profile = TestProfile.Create();
            if (prepare is not null) await prepare(simulator, profile);
            var scenario = new AppScenario(simulator, profile, useEnvironmentCredential);
            await scenario.LaunchAppAsync(cancellationToken);
            return scenario;
        }
        catch
        {
            if (profile is not null) await profile.DisposeAsync();
            await simulator.DisposeAsync();
            throw;
        }
    }

    /// <summary>Closes the app and starts it again on the same profile, as a user reopening it would.</summary>
    public async Task RestartAppAsync(CancellationToken cancellationToken)
    {
        await CloseOverlayPagesAsync();
        await App.DisposeAsync();
        await LaunchAppAsync(cancellationToken);
    }

    /// <summary>
    /// Opens the overlay in a headless Edge, as an OBS browser source would. The page counts as a connected
    /// overlay until the test ends.
    /// </summary>
    public async Task<OverlayPage> OpenOverlayAsync()
    {
        _overlayBrowser ??= await OverlayBrowser.LaunchAsync();
        return new OverlayPage(await _overlayBrowser.OpenAsync(App.OverlayUri));
    }

    /// <summary>
    /// Puts the running app back where a fresh launch leaves it, and empties the simulator, for the next test:
    /// the winner dialog dismissed, the channel unloaded, a newly created Dashboard. Returns null when it did, or,
    /// having changed nothing it cannot undo, why the app holds state that only a fresh profile clears: saved
    /// settings or credential, a page other than the Dashboard (an unsaved Settings draft, a dialog, Setup), or
    /// overlay layout the Dashboard does not re-send (a collapsed or resized played list, a revealed winner).
    /// </summary>
    public async Task<string?> TryResetAsync(CancellationToken cancellationToken)
    {
        await CloseOverlayPagesAsync();
        if (Profile.ReadSavedStateFingerprint() != _savedStateAtLaunch) return "the app saved settings or a credential";
        if (!IsDashboardUrl(Page.Url)) return $"the app is not on the Dashboard but at {Page.Url}";
        if (await Page.Locator(".mud-dialog").CountAsync() > 0) return "a dialog is open";

        var dashboard = Dashboard;
        if (await dashboard.Winner.Root.IsVisibleAsync()) await dashboard.Winner.EscapeAsync();
        if (await dashboard.ChangeButton.IsVisibleAsync())
        {
            await Expect(dashboard.ChangeButton).ToBeEnabledAsync();
            await dashboard.ChangeChannelAsync();
        }
        else if (await dashboard.StreamerLabel.IsVisibleAsync())
        {
            return "the loaded channel cannot be changed";
        }

        Simulator.Reset();

        // A new Dashboard component starts with an empty status, the wheel shown and the list expanded.
        await Settings.OpenAsync();
        await dashboard.OpenAsync();
        await Expect(dashboard.StreamerInput).ToHaveValueAsync("");
        await Expect(dashboard.Health.Channel).ToHaveTextAsync("Not loaded");

        return await HasDefaultOverlayLayoutAsync(cancellationToken)
            ? null
            : "the overlay keeps a collapsed or resized played list, or a winner";
    }

    /// <summary>Replaces the app with a new one on a fresh profile, against the same simulator, emptied.</summary>
    public async Task RelaunchOnFreshProfileAsync(CancellationToken cancellationToken)
    {
        await CloseOverlayPagesAsync();
        await App.DisposeAsync();
        await Profile.DisposeAsync();
        Simulator.Reset();
        Profile = TestProfile.Create();
        await LaunchAppAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_overlayBrowser is not null) await _overlayBrowser.DisposeAsync();
        }
        finally
        {
            try
            {
                if (App is not null) await App.DisposeAsync();
            }
            finally
            {
                try
                {
                    await Profile.DisposeAsync();
                }
                finally
                {
                    await Simulator.DisposeAsync();
                }
            }
        }
    }

    private async Task LaunchAppAsync(CancellationToken cancellationToken)
    {
        App = await DesktopApp.LaunchAsync(Simulator, Profile, cancellationToken, _useEnvironmentCredential);
        _savedStateAtLaunch = Profile.ReadSavedStateFingerprint();
    }

    private async Task CloseOverlayPagesAsync()
    {
        if (_overlayBrowser is not null) await _overlayBrowser.CloseAllPagesAsync();
    }

    private async Task<bool> HasDefaultOverlayLayoutAsync(CancellationToken cancellationToken)
    {
        await using var overlay = await OverlayEventStream.ConnectAsync(App.OverlayEventsUri, cancellationToken);
        var state = await overlay.NextAsync("init_state", cancellationToken);
        return !state.GetProperty("playedListCollapsed").GetBoolean() &&
               state.GetProperty("playedListWidth").GetString() is "" &&
               state.GetProperty("wheelVisible").GetBoolean() &&
               state.GetProperty("winner").ValueKind == System.Text.Json.JsonValueKind.Null;
    }

    private static bool IsDashboardUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.AbsolutePath is "/" or "/dashboard";
}
