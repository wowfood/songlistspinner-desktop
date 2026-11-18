using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Simulator;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Services;

/// <remarks>
/// The session's clock is never advanced, so its realtime refresh never runs; the test refreshes explicitly.
/// </remarks>
public class WheelSpinServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ExcludePlayedSongsForTheWeek_When_SpinningAndMarkingTheWinnerPlayed_Then_OnlyAnUnplayedSongWinsAndLeavesTheQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel(Streamer);
        var playedThisWeek = await channel.RequestSongAsync("Daft Punk", "Get Lucky", "early_bird");
        await channel.AddPlayedSongAsync("Daft Punk", "Get Lucky", Now.AddHours(-1));
        var playedLongAgo = await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        await channel.AddPlayedSongAsync("Toto", "Africa", Now.AddDays(-60));
        var config = new SpinnerConfig
        {
            PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true, Period = "week" }
        };
        await using var app = new SessionHarness(simulator, clock, new FakeTimeProvider(Now));
        await app.Loader.LoadAsync(Streamer, config, cancellationToken);

        var draw = await app.Spins.DrawAsync(Streamer, config, cancellationToken);
        app.Spins.Start(draw);
        await app.WinnerActions.MarkPlayedAsync(draw.Winner!.QueueId, cancellationToken);
        app.Spins.Finish();
        var refreshed = await app.Session.RefreshAsync(Streamer, cancellationToken);

        Assert.Equal([playedLongAgo.QueueId], draw.AvailableSongs.Select(song => song.QueueId));
        var historyRequest = simulator.Requests.Last(request => request.Path == "/play_history");
        Assert.Equal(Now.AddDays(-7).ToString("O"), historyRequest.Query["played_after"]);
        Assert.Equal([playedThisWeek.QueueId], channel.Queue.Select(entry => entry.QueueId));
        Assert.NotNull(refreshed);
        Assert.Empty(refreshed.AvailableSongs);
        Assert.Equal(["Africa", "Get Lucky"], refreshed.PlayedSongs.Select(song => song.Song?.Title));
    }
}
