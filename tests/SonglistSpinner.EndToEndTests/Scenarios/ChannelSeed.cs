using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests.Scenarios;

/// <summary>
/// Builds one simulator channel from readable data: a Now Playing request, the upcoming queue in order and play
/// history stamped relative to now. <see cref="ApplyAsync"/> seeds it and returns what was seeded, ids included,
/// so a test can state its expected values from the seed.
/// </summary>
/// <example>
/// <code>
/// var channel = await new ChannelSeed("seeded_streamer")
///     .WithNowPlaying(SongCatalog.Dreams)
///     .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
///     .WithQueued(SongCatalog.TakeOnMe, SongCatalog.MrBrightside)
///     .ApplyAsync(simulator);
/// </code>
/// </example>
internal sealed class ChannelSeed(string name, string platform = "twitch")
{
    private readonly List<SongSeed> _queued = [];
    private readonly List<(SongSeed Song, TimeSpan Ago)> _played = [];
    private SongSeed? _nowPlaying;

    public ChannelSeed WithNowPlaying(SongSeed song)
    {
        _nowPlaying = song;
        return this;
    }

    /// <summary>Adds upcoming requests, in queue order after any added before.</summary>
    public ChannelSeed WithQueued(params SongSeed[] songs)
    {
        _queued.AddRange(songs);
        return this;
    }

    /// <summary>
    /// Adds a song played <paramref name="ago"/> before the seed is applied. Keep a wide margin from a period's
    /// edge (Last 24 hours, Last 7 days, Last month): the app measures the period from its own clock a moment later.
    /// </summary>
    public ChannelSeed WithPlayed(SongSeed song, TimeSpan ago)
    {
        _played.Add((song, ago));
        return this;
    }

    /// <exception cref="InvalidOperationException">
    /// Two queue entries (Now Playing included) share an artist and title, so a test could not tell from the
    /// winner dialog which one won.
    /// </exception>
    public async Task<SeededChannel> ApplyAsync(StreamerSongListSimulator simulator)
    {
        var entries = (_nowPlaying is null ? _queued : _queued.Prepend(_nowPlaying)).ToList();
        var duplicate = entries
            .GroupBy(song => (song.Artist.ToUpperInvariant(), song.Title.ToUpperInvariant()))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"\"{duplicate.First().Artist} - {duplicate.First().Title}\" is queued more than once; seed unique songs.");

        var channel = simulator.AddChannel(name, platform);
        var now = DateTimeOffset.UtcNow;
        var played = new List<SimulatedPlayedSong>();
        foreach (var (song, ago) in _played)
            played.Add(await channel.AddPlayedSongAsync(song.Artist, song.Title, now - ago, song.Requester, song.Donation));

        SimulatedQueueEntry? nowPlaying = null;
        if (_nowPlaying is { } playing)
        {
            nowPlaying = await Request(channel, playing);
            await channel.SetNowPlayingAsync(nowPlaying.QueueId);
        }

        var queued = new List<SimulatedQueueEntry>();
        foreach (var song in _queued)
            queued.Add(await Request(channel, song));

        return new SeededChannel(channel, nowPlaying, queued, played);
    }

    private static Task<SimulatedQueueEntry> Request(SimulatedChannel channel, SongSeed song) =>
        channel.RequestSongAsync(song.Artist, song.Title, song.Requester, song.Donation);
}
