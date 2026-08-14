using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

public class OverlayStateService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private List<SpinnerQueueItem> _availableSongs = [];

    private SpinnerConfig _config = new();
    private string _currentStreamer = "";
    private bool _playedListCollapsed;
    private string _playedListMinWidth = "";
    private string _playedListWidth = "";
    private SpinnerQueueItem? _nowPlaying;
    private PlayHistoryItem[] _playedSongs = [];
    public int Port { get; } = 5150;
    public string OverlayUrl => $"http://localhost:{Port}/overlay";

    public Task UpdateStateAsync(
        SpinnerConfig config,
        List<SpinnerQueueItem> available,
        PlayHistoryItem[] played,
        SpinnerQueueItem? nowPlaying,
        string streamer)
    {
        _config = config;
        _availableSongs = available;
        _playedSongs = played;
        _nowPlaying = nowPlaying;
        _currentStreamer = streamer;

        var wheelItems = BuildWheelItems(available);
        var playedTexts = played.Select(s => SpinnerDataService.CreatePlayedSongText(s, config)).ToList();
        var nowPlayingText = BuildNowPlayingText(nowPlaying, config);

        return BroadcastAsync("update_songs", new
        {
            config,
            streamer,
            wheelItems,
            playedTexts,
            nowPlayingText,
            playedCount = played.Length,
            availableCount = available.Count
        });
    }

    public Task BroadcastSpinCommandAsync(int winnerIndex, int duration, string mainLine, string details)
    {
        return BroadcastAsync("spin_command", new { winnerIndex, duration, mainLine, details });
    }

    public Task BroadcastCloseWinnerAsync()
    {
        return BroadcastAsync("close_winner", new { });
    }

    public Task UpdatePlayedListCollapsedAsync(bool collapsed)
    {
        _playedListCollapsed = collapsed;
        return BroadcastAsync("set_collapse", new { collapsed });
    }

    public Task UpdatePlayedListWidthAsync(string width, string minWidth)
    {
        _playedListWidth = width;
        _playedListMinWidth = minWidth;
        return BroadcastAsync("set_played_list_width", new { width, minWidth });
    }

    public Task BroadcastAsync(string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var message = $"event: {eventName}\ndata: {json}\n\n";
        foreach (var (_, channel) in _clients)
            channel.Writer.TryWrite(message);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> SubscribeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        var key = Guid.NewGuid();
        _clients[key] = channel;

        try
        {
            yield return BuildInitStateEvent();

            await foreach (var msg in channel.Reader.ReadAllAsync(ct))
                yield return msg;
        }
        finally
        {
            _clients.TryRemove(key, out _);
            channel.Writer.TryComplete();
        }
    }

    private string BuildInitStateEvent()
    {
        var wheelItems = BuildWheelItems(_availableSongs);
        var playedTexts = _playedSongs.Select(s => SpinnerDataService.CreatePlayedSongText(s, _config)).ToList();
        var nowPlayingText = BuildNowPlayingText(_nowPlaying, _config);

        var payload = new
        {
            config = _config,
            streamer = _currentStreamer,
            wheelItems,
            playedTexts,
            nowPlayingText,
            playedCount = _playedSongs.Length,
            availableCount = _availableSongs.Count,
            playedListCollapsed = _playedListCollapsed,
            playedListWidth = _playedListWidth,
            playedListMinWidth = _playedListMinWidth
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        return $"event: init_state\ndata: {json}\n\n";
    }

    private static object[] BuildWheelItems(List<SpinnerQueueItem> songs)
    {
        return songs.Count > 0
            ? songs.Select(s => (object)new { label = SpinnerDataService.BuildWheelLabel(s) }).ToArray()
            : [new { label = "Waiting for Dashboard..." }];
    }

    private static string? BuildNowPlayingText(SpinnerQueueItem? item, SpinnerConfig config)
    {
        if (item is null) return null;
        var fields = config.NowPlaying.Fields is { Length: > 0 }
            ? config.NowPlaying.Fields
            : ["artist", "title"];
        return SpinnerDataService.CreateSongTextForFields(item, fields);
    }
}
