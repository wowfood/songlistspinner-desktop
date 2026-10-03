using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The Dashboard's service-health strip: API, Realtime, Overlay, Channel and the API environment. Each test starts
/// its own app, because the overlay count includes every event stream still open on the app's overlay server, and
/// the reset between a shared app's tests opens one.
/// </summary>
public class ServiceHealthTests
{
    /// <summary>
    /// The overlay server (an HttpListener) learns that a browser source left only when writing to it fails. It
    /// writes a keep-alive every 15 s, and the first write after the browser closes still succeeds, so a departure
    /// shows at the second keep-alive: 30 s after the streams below connect, as measured. The limit leaves 15 s over.
    /// </summary>
    private const float DepartureLimitMilliseconds = 45_000;

    [Fact(Timeout = 180_000)]
    public async Task Given_NoChannelLoaded_When_TheDashboardOpens_Then_TheHealthStripShowsNothingConnectedAndTheOverlayReady()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(cancellationToken);

        var health = scenario.Dashboard.Health;
        await Expect(health.Api).ToHaveTextAsync("Not connected");
        await health.ExpectApiDetailAsync("Waiting for a channel to be loaded.");
        await Expect(health.Realtime).ToHaveTextAsync("Not connected");
        await health.ExpectRealtimeDetailAsync("Waiting for a channel to be loaded.");
        await Expect(health.Channel).ToHaveTextAsync("Not loaded");
        // The simulator is neither the production nor the staging host.
        await Expect(health.Environment).ToHaveTextAsync("Custom API");
        await Expect(health.Environment).ToHaveAttributeAsync("title", scenario.Simulator.ApiBaseAddress.ToString());
        await Expect(health.Overlay).ToHaveTextAsync("Ready");
        await health.ExpectOverlayDetailAsync($"{scenario.App.OverlayUri} — 0 connected browser source(s).");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheQueueResponseIsHeld_When_LoadingAChannelAndThenAnsweringIt_Then_TheApiGoesFromCheckingToConnected()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        await new ChannelSeed("health_streamer")
            .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
            .ApplyAsync(scenario.Simulator);
        var hold = scenario.Simulator.HoldNextRequest(HttpMethod.Get, "/queue");
        var dashboard = scenario.Dashboard;
        var health = dashboard.Health;

        await dashboard.StartLoadingChannelAsync("health_streamer");
        await hold.Arrived.WaitAsync(cancellationToken);

        await Expect(health.Api).ToHaveTextAsync("Checking");
        await health.ExpectApiDetailAsync("Resolving health_streamer and loading its queue.");
        await Expect(dashboard.Status).ToHaveTextAsync("Loading songs...");

        hold.Release();

        await Expect(health.Api).ToHaveTextAsync("Connected");
        // The detail carries the local time in the app's culture.
        await health.ExpectApiDetailAsync(new Regex(@"^Queue and history last synchronized at .+\.$"));
        await Expect(health.Realtime).ToHaveTextAsync("Connected");
        await health.ExpectRealtimeDetailAsync("Receiving live queue and history events.");
        await Expect(health.Channel).ToHaveTextAsync("health_streamer");
        await Expect(dashboard.Status).ToHaveTextAsync("Loaded 2 songs. Press SPIN!");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDashboardIsOpen_When_OverlayBrowserSourcesConnect_Then_TheOverlayHealthCountsThem()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var health = scenario.Dashboard.Health;
        await Expect(health.Overlay).ToHaveTextAsync("Ready");

        await using var first = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);

        await Expect(health.Overlay).ToHaveTextAsync("1 connected");
        await health.ExpectOverlayDetailAsync($"{scenario.App.OverlayUri} — 1 connected browser source(s).");

        await using var second = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);

        await Expect(health.Overlay).ToHaveTextAsync("2 connected");
        await health.ExpectOverlayDetailAsync($"{scenario.App.OverlayUri} — 2 connected browser source(s).");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TwoConnectedOverlayBrowserSources_When_BothLeave_Then_TheOverlayHealthReturnsToReady()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var health = scenario.Dashboard.Health;
        var first = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        var second = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        await Expect(health.Overlay).ToHaveTextAsync("2 connected");

        await first.DisposeAsync();
        await second.DisposeAsync();

        await Expect(health.Overlay).ToHaveTextAsync(
            "Ready",
            new LocatorAssertionsToHaveTextOptions { Timeout = DepartureLimitMilliseconds });
        await health.ExpectOverlayDetailAsync($"{scenario.App.OverlayUri} — 0 connected browser source(s).");
    }
}
