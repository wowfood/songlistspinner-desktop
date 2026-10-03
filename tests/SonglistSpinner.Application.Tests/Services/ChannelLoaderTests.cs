using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;
using Xunit;
using static SonglistSpinner.Application.Tests.ScriptedStreamerSongListClient;

namespace SonglistSpinner.Application.Tests.Services;

public class ChannelLoaderTests
{
    private const string Streamer = "wowfood";

    private static readonly SpinnerConfig ExcludePlayedOnYouTube = new()
    {
        Streamer = new SpinnerStreamerConfig { Platform = "youtube" },
        PlayHistory = new SpinnerPlayHistoryConfig { ExcludePlayedSongs = true }
    };

    [Fact]
    public async Task Given_StreamerResolves_When_Loading_Then_ReturnsTheLoadedChannel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = ApiResolvingStreamerAs(77);
        await using var session = NewSession(api);
        var loader = new ChannelLoader(api, session);

        var loaded = await loader.LoadAsync(Streamer, ExcludePlayedOnYouTube, cancellationToken);

        Assert.Equal(new LoadedChannel(new StreamerId(77), Streamer), loaded);
    }

    [Fact]
    public async Task Given_StreamerResolves_When_Loading_Then_ResolvesTheNameOnTheConfiguredPlatform()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resolved = new List<StreamerSongListChannel>();
        var api = ApiResolvingStreamerAs(77, resolved);
        await using var session = NewSession(api);
        var loader = new ChannelLoader(api, session);

        await loader.LoadAsync(Streamer, ExcludePlayedOnYouTube, cancellationToken);

        Assert.Equal([new StreamerSongListChannel(Streamer, "youtube")], resolved);
    }

    [Fact]
    public async Task Given_PlayedSongInTheQueue_When_Loading_Then_SessionStartsWithOnlyTheAvailableSongs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = ApiResolvingStreamerAs(77);
        var nowPlaying = Song(40);
        api.QueueResponses.Enqueue(_ => Task.FromResult(new SpinnerQueueSnapshot
        {
            Items = [SongWithId(41, songId: 500), SongWithId(42, songId: 501)],
            Playing = nowPlaying
        }));
        var played = new PlayHistoryItem { Song = new SpinnerSong { Id = 500 } };
        api.PlayHistory = [played];
        await using var session = NewSession(api);
        var loader = new ChannelLoader(api, session);

        await loader.LoadAsync(Streamer, ExcludePlayedOnYouTube, cancellationToken);

        var snapshot = session.GetSnapshot();
        Assert.Equal(new LoadedChannel(new StreamerId(77), Streamer), snapshot.Channel);
        Assert.Equal([42], snapshot.AvailableSongs.Select(song => song.QueueId));
        Assert.Equal([played], snapshot.PlayedSongs);
        Assert.Same(nowPlaying, snapshot.NowPlaying);
        Assert.Same(ExcludePlayedOnYouTube, snapshot.Config);
    }

    [Fact]
    public async Task Given_StreamerCannotBeResolved_When_Loading_Then_ThrowsAndKeepsThePreviousChannel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient
        {
            ResolveStreamer = _ => throw new HttpRequestException("Streamer not found")
        };
        await using var session = NewSession(api);
        await session.StartAsync(12, "previous", new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        var loader = new ChannelLoader(api, session);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => loader.LoadAsync(Streamer, ExcludePlayedOnYouTube, cancellationToken));

        Assert.Equal("Streamer not found", error.Message);
        var snapshot = session.GetSnapshot();
        Assert.Equal(new LoadedChannel(new StreamerId(12), "previous"), snapshot.Channel);
        Assert.Equal([7], snapshot.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_QueueFetchFails_When_Loading_Then_ThrowsAndKeepsThePreviousChannel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = ApiResolvingStreamerAs(77);
        api.QueueResponses.Enqueue(_ => throw new IOException("Queue unavailable"));
        await using var session = NewSession(api);
        await session.StartAsync(12, "previous", new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        var loader = new ChannelLoader(api, session);

        var error = await Assert.ThrowsAsync<IOException>(
            () => loader.LoadAsync(Streamer, ExcludePlayedOnYouTube, cancellationToken));

        Assert.Equal("Queue unavailable", error.Message);
        Assert.Equal(new LoadedChannel(new StreamerId(12), "previous"), session.GetSnapshot().Channel);
    }

    private static ScriptedStreamerSongListClient ApiResolvingStreamerAs(
        int streamerId,
        List<StreamerSongListChannel>? resolved = null)
    {
        return new ScriptedStreamerSongListClient
        {
            ResolveStreamer = channel =>
            {
                resolved?.Add(channel);
                return Task.FromResult(new StreamerSongListStreamer(new StreamerId(streamerId), []));
            }
        };
    }

    private static StreamerSessionService NewSession(ScriptedStreamerSongListClient api) =>
        new(api, new ChannelEventSource(), new OverlayStateService());

    private static SpinnerQueueItem SongWithId(int queueId, int songId) =>
        new() { QueueId = queueId, Song = new SpinnerSong { Id = songId } };
}
