using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Simulator;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Services;

/// <remarks>
/// The session's clock is never advanced here, so its own realtime refresh never runs; each test refreshes
/// explicitly and sees exactly the state the action left.
/// </remarks>
public class WinnerActionServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ALoadedChannel_When_MarkPlayedAsync_Then_TheWinnerMovesToPlayHistoryAndTheNextRefreshShowsIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel(Streamer);
        var winner = await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        var other = await channel.RequestSongAsync("a-ha", "Take On Me", "synth_lover");
        await using var app = new SessionHarness(simulator, clock, new FakeTimeProvider(Now));
        await app.Loader.LoadAsync(Streamer, new SpinnerConfig(), cancellationToken);

        await app.WinnerActions.MarkPlayedAsync(winner.QueueId, cancellationToken);
        var refreshed = await app.Session.RefreshAsync(Streamer, cancellationToken);

        Assert.Equal([other.QueueId], channel.Queue.Select(entry => entry.QueueId));
        var played = Assert.Single(channel.PlayHistory);
        Assert.Equal(("Africa", Now), (played.Title, played.PlayedAt));
        Assert.NotNull(refreshed);
        Assert.Equal([other.QueueId], refreshed.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(["Africa"], refreshed.PlayedSongs.Select(song => song.Song?.Title));
    }

    [Fact]
    public async Task Given_ASongIsPlaying_When_PromoteToNowPlayingAsync_Then_ThePreviousSongIsPlayedAndTheWinnerIsNowPlaying()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel(Streamer);
        var previous = await channel.RequestSongAsync("Fleetwood Mac", "Dreams", "night_owl");
        await channel.SetNowPlayingAsync(previous.QueueId);
        var first = await channel.RequestSongAsync("a-ha", "Take On Me", "synth_lover");
        var winner = await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        await using var app = new SessionHarness(simulator, clock, new FakeTimeProvider(Now));
        await app.Loader.LoadAsync(Streamer, new SpinnerConfig(), cancellationToken);

        await app.WinnerActions.PromoteToNowPlayingAsync(winner.QueueId, cancellationToken);
        var refreshed = await app.Session.RefreshAsync(Streamer, cancellationToken);

        Assert.Equal(winner.QueueId, channel.NowPlaying?.QueueId);
        Assert.Equal(["Dreams"], channel.PlayHistory.Select(song => song.Title));
        Assert.NotNull(refreshed);
        Assert.Equal(winner.QueueId, refreshed.NowPlaying?.QueueId);
        Assert.Equal([first.QueueId], refreshed.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(["Dreams"], refreshed.PlayedSongs.Select(song => song.Song?.Title));
    }
}
