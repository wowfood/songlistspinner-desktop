using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

public class OverlayStateService
{
    private const int ClientBufferCapacity = 32;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private readonly object _healthGate = new();
    private readonly object _stateGate = new();
    private OverlaySnapshot _snapshot = OverlaySnapshot.Empty;
    private string? _serverError;
    private LocalOverlayServerState _serverState = LocalOverlayServerState.Stopped;

    public event EventHandler? HealthChanged;

    public int Port { get; } = 5150;
    public string OverlayUrl => $"http://localhost:{Port}/overlay";

    public LocalOverlayHealth GetHealth()
    {
        lock (_healthGate)
        {
            return new LocalOverlayHealth(_serverState, _clients.Count, _serverError);
        }
    }

    internal void SetServerHealth(LocalOverlayServerState state, string? error = null)
    {
        lock (_healthGate)
        {
            _serverState = state;
            _serverError = error;
        }

        OnHealthChanged();
    }

    public Task UpdateStateAsync(
        SpinnerConfig config,
        List<SpinnerQueueItem> available,
        PlayHistoryItem[] played,
        SpinnerQueueItem? nowPlaying,
        string streamer)
    {
        OverlaySnapshot snapshot;
        lock (_stateGate)
        {
            snapshot = _snapshot = _snapshot with
            {
                Config = config,
                AvailableSongs = [.. available],
                PlayedSongs = [.. played],
                NowPlaying = nowPlaying,
                CurrentStreamer = streamer
            };
        }

        return BroadcastStateAsync(snapshot);
    }

    public Task UpdateConfigAsync(SpinnerConfig config)
    {
        OverlaySnapshot snapshot;
        lock (_stateGate)
            snapshot = _snapshot = _snapshot with { Config = config };

        return BroadcastStateAsync(snapshot);
    }

    private Task BroadcastStateAsync(OverlaySnapshot snapshot)
    {
        return BroadcastAsync("update_songs", new
        {
            config = snapshot.Config,
            streamer = snapshot.CurrentStreamer,
            wheelItems = BuildWheelItems(snapshot.AvailableSongs),
            playedTexts = snapshot.PlayedSongs
                .Select(song => SpinnerDataService.CreatePlayedSongText(song, snapshot.Config))
                .ToList(),
            nowPlayingText = BuildNowPlayingText(snapshot.NowPlaying, snapshot.Config),
            playedCount = snapshot.PlayedSongs.Length,
            availableCount = snapshot.AvailableSongs.Length
        });
    }

    public Task BroadcastSpinCommandAsync(
        int winnerIndex,
        int winnerQueueId,
        int duration,
        string mainLine,
        string details)
    {
        return BroadcastAsync("spin_command", new { winnerIndex, winnerQueueId, duration, mainLine, details });
    }

    public Task BroadcastCloseWinnerAsync()
    {
        return BroadcastAsync("close_winner", new { });
    }

    public Task UpdatePlayedListCollapsedAsync(bool collapsed)
    {
        lock (_stateGate)
            _snapshot = _snapshot with { PlayedListCollapsed = collapsed };
        return BroadcastAsync("set_collapse", new { collapsed });
    }

    public Task UpdatePlayedListWidthAsync(string width, string minWidth)
    {
        lock (_stateGate)
            _snapshot = _snapshot with { PlayedListWidth = width, PlayedListMinWidth = minWidth };
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
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ClientBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });
        var key = Guid.NewGuid();
        _clients[key] = channel;
        OnHealthChanged();

        try
        {
            yield return BuildInitStateEvent();

            Task<bool>? messageAvailable = null;
            while (!ct.IsCancellationRequested)
            {
                messageAvailable ??= channel.Reader.WaitToReadAsync(ct).AsTask();
                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var heartbeatDue = Task.Delay(TimeSpan.FromSeconds(15), heartbeatCts.Token);
                var completed = await Task.WhenAny(messageAvailable, heartbeatDue);

                if (completed == heartbeatDue)
                {
                    ct.ThrowIfCancellationRequested();
                    // SSE comments keep quiet browser sources alive and make disconnects observable.
                    yield return ": keep-alive\n\n";
                    continue;
                }

                await heartbeatCts.CancelAsync();
                if (!await messageAvailable) break;
                messageAvailable = null;
                while (channel.Reader.TryRead(out var message))
                    yield return message;
            }
        }
        finally
        {
            if (_clients.TryRemove(key, out _)) OnHealthChanged();
            channel.Writer.TryComplete();
        }
    }

    private string BuildInitStateEvent()
    {
        OverlaySnapshot snapshot;
        lock (_stateGate)
            snapshot = _snapshot;

        var wheelItems = BuildWheelItems(snapshot.AvailableSongs);
        var playedTexts = snapshot.PlayedSongs
            .Select(song => SpinnerDataService.CreatePlayedSongText(song, snapshot.Config))
            .ToList();
        var nowPlayingText = BuildNowPlayingText(snapshot.NowPlaying, snapshot.Config);

        var payload = new
        {
            config = snapshot.Config,
            streamer = snapshot.CurrentStreamer,
            wheelItems,
            playedTexts,
            nowPlayingText,
            playedCount = snapshot.PlayedSongs.Length,
            availableCount = snapshot.AvailableSongs.Length,
            playedListCollapsed = snapshot.PlayedListCollapsed,
            playedListWidth = snapshot.PlayedListWidth,
            playedListMinWidth = snapshot.PlayedListMinWidth
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        return $"event: init_state\ndata: {json}\n\n";
    }

    private static object[] BuildWheelItems(IReadOnlyCollection<SpinnerQueueItem> songs)
    {
        return songs.Count > 0
            ? songs.Select(song => (object)new
            {
                queueId = song.QueueId,
                label = SpinnerDataService.BuildWheelLabel(song)
            }).ToArray()
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

    private void OnHealthChanged()
    {
        var handlers = HealthChanged;
        if (handlers is null) return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[OverlayState] A health observer failed: {ex}");
            }
        }
    }

    private sealed record OverlaySnapshot(
        SpinnerConfig Config,
        SpinnerQueueItem[] AvailableSongs,
        PlayHistoryItem[] PlayedSongs,
        SpinnerQueueItem? NowPlaying,
        string CurrentStreamer,
        bool PlayedListCollapsed,
        string PlayedListWidth,
        string PlayedListMinWidth)
    {
        public static OverlaySnapshot Empty { get; } = new(
            new SpinnerConfig(),
            [],
            [],
            null,
            "",
            false,
            "",
            "");
    }
}
