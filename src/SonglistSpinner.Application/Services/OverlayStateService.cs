using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.PlayedSongs;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.Songs;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.Winner;

namespace SonglistSpinner.Services;

public class OverlayStateService
{
    /// <summary>How many events a connected overlay may fall behind before it loses the oldest.</summary>
    internal const int ClientBufferCapacity = 32;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, Channel<OverlayEvent>> _clients = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OverlayStateService> _logger;

    // Every change to the overlay snapshot is recorded and broadcast while holding this lock, and
    // SubscribeAsync takes it to build a client's initial state and register the client. So clients
    // receive changes in the order the snapshot recorded them, and a connecting client sees either the
    // old state followed by the change, or the new state. Broadcasting only queues messages on bounded
    // channels and never blocks.
    private readonly object _stateGate = new();
    private OverlaySnapshot _snapshot = OverlaySnapshot.Empty;

    public OverlayStateService(TimeProvider? timeProvider = null, ILogger<OverlayStateService>? logger = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<OverlayStateService>.Instance;
    }

    /// <summary>Raised after an overlay connects or disconnects.</summary>
    public event EventHandler? ConnectedClientsChanged;

    public int ConnectedClientCount => _clients.Count;


    public void UpdateState(
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
            BroadcastState(_snapshot);
        }
    }

    public void UpdateConfig(SpinnerConfig config)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Config = config };
            BroadcastState(_snapshot);
        }
    }

    private void BroadcastState(OverlaySnapshot snapshot)
    {
        Broadcast(OverlayEventNames.UpdateSongs, CreateStatePayload(snapshot));
    }

    public void BroadcastSpinCommand(
        int winnerIndex,
        int winnerQueueId,
        int duration)
    {
        Broadcast(OverlayEventNames.SpinCommand, new { winnerIndex, winnerQueueId, duration });
    }

    // Winner and wheel visibility are recorded so a reconnecting overlay replays them in its initial state.
    public void BroadcastWinnerReveal(
        IReadOnlyList<WinnerDialogField> fields,
        int? queuePosition)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Winner = new WinnerSnapshot([.. fields], queuePosition) };
            Broadcast(OverlayEventNames.WinnerReveal, _snapshot.Winner);
        }
    }

    public void BroadcastCloseWinner()
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Winner = null };
            Broadcast(OverlayEventNames.CloseWinner, new { });
        }
    }

    public void BroadcastWheelVisibility(bool visible)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { WheelVisible = visible };
            Broadcast(OverlayEventNames.SetWheelVisible, new { visible });
        }
    }

    public void UpdatePlayedListCollapsed(bool collapsed)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { PlayedListCollapsed = collapsed };
            Broadcast(OverlayEventNames.SetCollapse, new { collapsed });
        }
    }

    public void UpdatePlayedListWidth(string width, string minWidth)
    {
        lock (_stateGate)
        {
            _snapshot = _snapshot with { PlayedListWidth = width, PlayedListMinWidth = minWidth };
            Broadcast(OverlayEventNames.SetPlayedListWidth, new { width, minWidth });
        }
    }

    private void Broadcast(string eventName, object payload)
    {
        var message = OverlayEvent.Named(eventName, JsonSerializer.Serialize(payload, JsonOpts));
        foreach (var (_, channel) in _clients)
            channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Streams the overlay's events to one connected overlay: the initial state first, then each change, with a
    /// keep-alive after every quiet heartbeat interval. A client that falls a full buffer
    /// of events behind loses the oldest.
    /// </summary>
    public async IAsyncEnumerable<OverlayEvent> SubscribeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<OverlayEvent>(new BoundedChannelOptions(ClientBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });
        var key = Guid.NewGuid();
        OverlayEvent initialState;
        lock (_stateGate)
        {
            initialState = BuildInitStateEvent();
            _clients[key] = channel;
        }

        OnConnectedClientsChanged();

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
                    yield return OverlayEvent.KeepAlive;
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
            if (_clients.TryRemove(key, out _)) OnConnectedClientsChanged();
            channel.Writer.TryComplete();
        }
    }

    private OverlayEvent BuildInitStateEvent()
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

        return OverlayEvent.Named(OverlayEventNames.InitialState, JsonSerializer.Serialize(payload, JsonOpts));
    }

    private static OverlayStatePayload CreateStatePayload(OverlaySnapshot snapshot)
    {
        return new OverlayStatePayload(
            snapshot.Config,
            snapshot.CurrentStreamer,
            CreateWheelItems(snapshot.AvailableSongs),
            PlayedSongList.CreateTexts(snapshot.PlayedSongs, snapshot.Config),
            PlayedSongList.CreateFieldTable(snapshot.PlayedSongs, snapshot.Config),
            snapshot.NowPlaying is null
                ? null
                : SongDisplayText.CreateNowPlayingText(snapshot.NowPlaying, snapshot.Config.NowPlaying),
            snapshot.PlayedSongs.Length,
            snapshot.AvailableSongs.Length);
    }

    private static OverlayWheelItem[] CreateWheelItems(IReadOnlyCollection<SpinnerQueueItem> songs)
    {
        return songs.Count > 0
            ? songs.Select(song => new OverlayWheelItem(SongDisplayText.BuildWheelLabel(song))
            {
                QueueId = song.QueueId
            }).ToArray()
            : [new OverlayWheelItem("Waiting for Dashboard...")];
    }

    private void OnConnectedClientsChanged()
    {
        var handlers = ConnectedClientsChanged;
        if (handlers is null) return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An overlay connection observer failed");
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
