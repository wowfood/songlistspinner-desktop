using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Simulator;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Services;

public class ChannelLoaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ChannelWithAQueueAndHistory_When_LoadAsync_Then_TheSessionHoldsTheUnplayedSongsHistoryAndNowPlaying()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now);
        await using var simulator = await StreamerSongListSimulator.StartAsync(
            new StreamerSongListSimulatorOptions { TimeProvider = clock },
            cancellationToken);
        var channel = simulator.AddChannel("wowfood", streamerId: 314);
        await channel.AddPlayedSongAsync("Daft Punk", "Get Lucky", Now.AddHours(-1));
        var playing = await channel.RequestSongAsync("Fleetwood Mac", "Dreams", "night_owl");
        await channel.SetNowPlayingAsync(playing.QueueId);
        await channel.RequestSongAsync("daft punk", "get lucky", "early_bird");
        var unplayed = await channel.RequestSongAsync("Toto", "Africa", "long_time_fan");
        await using var app = new SessionHarness(simulator, clock, new FakeTimeProvider(Now));
        var config = new SpinnerConfig { PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true } };

        var loaded = await app.Loader.LoadAsync("wowfood", config, cancellationToken);

        var snapshot = app.Session.GetSnapshot();
        Assert.Equal(314, loaded.Id.Value);
        Assert.Equal(314, snapshot.StreamerId);
        Assert.Equal([unplayed.QueueId], snapshot.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal(["Get Lucky"], snapshot.PlayedSongs.Select(song => song.Song?.Title));
        Assert.Equal(playing.QueueId, snapshot.NowPlaying?.QueueId);
    }
}
