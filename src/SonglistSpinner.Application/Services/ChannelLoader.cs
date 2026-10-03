using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// Loads a StreamerSongList channel into the session: resolves the streamer, fetches its queue and play history,
/// filters the songs the wheel may land on and starts the session's realtime updates.
/// </summary>
public sealed class ChannelLoader
{
    private readonly IStreamerSongListClient _songListClient;
    private readonly StreamerSessionService _session;
    private readonly ILogger<ChannelLoader> _logger;

    public ChannelLoader(
        IStreamerSongListClient songListClient,
        StreamerSessionService session,
        ILogger<ChannelLoader>? logger = null)
    {
        _songListClient = songListClient;
        _session = session;
        _logger = logger ?? NullLogger<ChannelLoader>.Instance;
    }

    /// <summary>
    /// Loads <paramref name="streamerName"/> on the platform <paramref name="config"/> names. The session keeps
    /// the channel it already had until the resolve and both fetches succeed; any failure propagates.
    /// </summary>
    public async Task<LoadedChannel> LoadAsync(
        string streamerName,
        SpinnerConfig config,
        CancellationToken cancellationToken)
    {
        var channel = new StreamerSongListChannel(streamerName, config.Streamer.Platform);
        var streamer = await _songListClient.ResolveStreamerAsync(channel, cancellationToken);
        var queue = await _songListClient.FetchQueueAndHistoryAsync(
            channel,
            config.PlayHistory.Period,
            cancellationToken);
        var availableSongs = queue.AvailableSongs(config);

        await _session.StartAsync(
            streamer.Id.Value,
            channel.Name,
            config,
            availableSongs,
            queue.PlayedSongs,
            queue.Queue.Playing,
            cancellationToken);
        _logger.LogInformation(
            "Loaded channel {Streamer} (streamer {StreamerId}) with {AvailableSongCount} spinnable songs",
            channel.Name,
            streamer.Id.Value,
            availableSongs.Count);
        return new LoadedChannel(streamer.Id, channel.Name);
    }
}
