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
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private readonly object _healthGate = new();
    private readonly TimeProvider _timeProvider;

    // Every change to the overlay snapshot is recorded and broadcast while holding this lock, and
    // SubscribeAsync takes it to build a client's initial state and register the client. So clients
    // receive changes in the order the snapshot recorded them, and a connecting client sees either the
    // old state followed by the change, or the new state. Broadcasting only queues messages on bounded
    // channels and never blocks.
    private readonly object _stateGate = new();
    private OverlaySnapshot _snapshot = OverlaySnapshot.Empty;
    private string? _serverError;
    private LocalOverlayServerState _serverState = LocalOverlayServerState.Stopped;

    public OverlayStateService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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
        lock (_stateGate)
        {
            _snapshot = _snapshot with
            {
                Config = config,
                AvailableSongs = [.. available],
                PlayedSongs = [.. played],
                NowPlaying = nowPlaying,
                CurrentStreamer = streamer
            };
            return BroadcastStateAsync(_snapshot);
        }
    }

    public Task UpdateConfigAsync(SpinnerConfig config)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Config = config };
            return BroadcastStateAsync(_snapshot);
        }
    }

    private Task BroadcastStateAsync(OverlaySnapshot snapshot)
    {
        return BroadcastAsync(OverlayEventNames.UpdateSongs, CreateStatePayload(snapshot));
    }

    public Task BroadcastSpinCommandAsync(
        int winnerIndex,
        int winnerQueueId,
        int duration)
    {
        return BroadcastAsync(OverlayEventNames.SpinCommand, new { winnerIndex, winnerQueueId, duration });
    }

    // Winner and wheel visibility are recorded so a reconnecting overlay replays them in its initial state.
    public Task BroadcastWinnerRevealAsync(
        IReadOnlyList<WinnerDialogField> fields,
        int? queuePosition)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Winner = new WinnerSnapshot([.. fields], queuePosition) };
            return BroadcastAsync(OverlayEventNames.WinnerReveal, _snapshot.Winner);
        }
    }

    public Task BroadcastCloseWinnerAsync()
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Winner = null };
            return BroadcastAsync(OverlayEventNames.CloseWinner, new { });
        }
    }

    public Task BroadcastWheelVisibilityAsync(bool visible)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { WheelVisible = visible };
            return BroadcastAsync(OverlayEventNames.SetWheelVisible, new { visible });
        }
    }

    public Task UpdatePlayedListCollapsedAsync(bool collapsed)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { PlayedListCollapsed = collapsed };
            return BroadcastAsync(OverlayEventNames.SetCollapse, new { collapsed });
        }
    }

    public Task UpdatePlayedListWidthAsync(string width, string minWidth)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { PlayedListWidth = width, PlayedListMinWidth = minWidth };
            return BroadcastAsync(OverlayEventNames.SetPlayedListWidth, new { width, minWidth });
        }
    }

    private Task BroadcastAsync(string eventName, object payload)
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
        string initialState;
        lock (_stateGate)
        {
            initialState = BuildInitStateEvent();
            _clients[key] = channel;
        }

        OnHealthChanged();

        try
        {
            yield return initialState;

            Task<bool>? messageAvailable = null;
            while (!ct.IsCancellationRequested)
            {
                messageAvailable ??= channel.Reader.WaitToReadAsync(ct).AsTask();
                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var heartbeatDue = Task.Delay(HeartbeatInterval, _timeProvider, heartbeatCts.Token);
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

        var payload = new InitialStatePayload(CreateStatePayload(snapshot))
        {
            PlayedListCollapsed = snapshot.PlayedListCollapsed,
            PlayedListWidth = snapshot.PlayedListWidth,
            PlayedListMinWidth = snapshot.PlayedListMinWidth,
            WheelVisible = snapshot.WheelVisible,
            Winner = snapshot.Winner
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        return $"event: {OverlayEventNames.InitialState}\ndata: {json}\n\n";
    }

    private static OverlayStatePayload CreateStatePayload(OverlaySnapshot snapshot)
    {
        return new OverlayStatePayload(
            snapshot.Config,
            snapshot.CurrentStreamer,
            CreateWheelItems(snapshot.AvailableSongs),
            SpinnerDataService.CreatePlayedSongTexts(snapshot.PlayedSongs, snapshot.Config),
            SpinnerDataService.CreatePlayedSongFieldTable(snapshot.PlayedSongs, snapshot.Config),
            snapshot.NowPlaying is null
                ? null
                : SpinnerDataService.CreateNowPlayingText(snapshot.NowPlaying, snapshot.Config.NowPlaying),
            snapshot.PlayedSongs.Length,
            snapshot.AvailableSongs.Length);
    }

    private static OverlayWheelItem[] CreateWheelItems(IReadOnlyCollection<SpinnerQueueItem> songs)
    {
        return songs.Count > 0
            ? songs.Select(song => new OverlayWheelItem(SpinnerDataService.BuildWheelLabel(song))
            {
                QueueId = song.QueueId
            }).ToArray()
            : [new OverlayWheelItem("Waiting for Dashboard...")];
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

    private sealed record WinnerSnapshot(WinnerDialogField[] Fields, int? QueuePosition);

    // init_state is the overlay state plus the layout and winner a reconnecting overlay must replay.
    private sealed record InitialStatePayload : OverlayStatePayload
    {
        public InitialStatePayload(OverlayStatePayload state)
            : base(state)
        {
        }

        public bool PlayedListCollapsed { get; init; }
        public string PlayedListWidth { get; init; } = "";
        public string PlayedListMinWidth { get; init; } = "";
        public bool WheelVisible { get; init; }
        public WinnerSnapshot? Winner { get; init; }
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
        public bool WheelVisible { get; init; } = true;
        public WinnerSnapshot? Winner { get; init; }

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
