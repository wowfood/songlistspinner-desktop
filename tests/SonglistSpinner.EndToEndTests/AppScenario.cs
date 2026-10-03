using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// One test's world: the simulator seeded with the demo channel, a fresh test profile and the app running on
/// both. Disposing kills the app, deletes the profile and stops the simulator, whatever the test's outcome.
/// </summary>
internal sealed class AppScenario : IAsyncDisposable
{
    private AppScenario(StreamerSongListSimulator simulator, SimulatedChannel channel, TestProfile profile)
    {
        Simulator = simulator;
        Channel = channel;
        Profile = profile;
    }

    public StreamerSongListSimulator Simulator { get; }

    /// <summary>The demo channel (see the simulator's DemoSeed): Now Playing and six upcoming requests.</summary>
    public SimulatedChannel Channel { get; }

    public TestProfile Profile { get; }

    public DesktopApp App { get; private set; } = null!;

    public static async Task<AppScenario> StartAsync(CancellationToken cancellationToken)
    {
        var simulator = await StreamerSongListSimulator.StartAsync(cancellationToken: cancellationToken);
        TestProfile? profile = null;
        try
        {
            var channel = await DemoSeed.ApplyAsync(simulator, TimeProvider.System);
            profile = TestProfile.Create();
            var scenario = new AppScenario(simulator, channel, profile);
            scenario.App = await DesktopApp.LaunchAsync(simulator, profile, cancellationToken);
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
        await App.DisposeAsync();
        App = await DesktopApp.LaunchAsync(Simulator, Profile, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await App.DisposeAsync();
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
