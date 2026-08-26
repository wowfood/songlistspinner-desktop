using System.Diagnostics;
using System.Threading.Channels;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

/// <summary>
/// Owns the active StreamerSongList channel independently of the currently rendered page.
/// This keeps the local overlay synchronized while the user visits Settings or Setup.
/// </summary>
public sealed class StreamerSessionService : IAsyncDisposable
{
    private readonly ISpinnerApiService _apiService;
    private readonly IStreamerSongListEventSource _eventSource;
    private readonly OverlayStateService _overlayService;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _stateGate = new();

    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;
    private Task? _refreshTask;
    private Channel<bool>? _refreshSignals;
    private StreamerSessionSnapshot _snapshot = StreamerSessionSnapshot.Empty;
    private bool _refreshSuspended;

    public StreamerSessionService(
        ISpinnerApiService apiService,
        IStreamerSongListEventSource eventSource,
        OverlayStateService overlayService)
    {
        _apiService = apiService;
        _eventSource = eventSource;
        _overlayService = overlayService;
    }

    public event EventHandler<StreamerSessionChangedEventArgs>? Changed;

    public StreamerSessionSnapshot GetSnapshot()
    {
        lock (_stateGate)
            return _snapshot;
    }

    public async Task StartAsync(
        int streamerId,
        string streamer,
        SpinnerConfig config,
        IReadOnlyCollection<SpinnerQueueItem> availableSongs,
        IReadOnlyCollection<PlayHistoryItem> playedSongs,
        SpinnerQueueItem? nowPlaying,
        CancellationToken cancellationToken = default)
    {
        await StopSubscriptionAsync();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateGate)
        {
            _snapshot = new StreamerSessionSnapshot(
                streamerId,
                streamer,
                config,
                availableSongs.ToArray(),
                playedSongs.ToArray(),
                nowPlaying,
                StreamerSessionHealth.Healthy,
                $"Queue and history last synchronized at {DateTime.Now:t}.",
                StreamerSessionHealth.Checking,
                $"Connecting to realtime updates for {streamer}.");
        }

        await _overlayService.UpdateStateAsync(
            config,
            availableSongs.ToList(),
            playedSongs.ToArray(),
            nowPlaying,
            streamer);
        RaiseChanged();

        _subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _refreshSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var subscriptionToken = _subscriptionCts.Token;
        _subscriptionTask = RunEventsAsync(streamerId, _refreshSignals.Writer, subscriptionToken);
        _refreshTask = RunRefreshesAsync(streamer, _refreshSignals.Reader, subscriptionToken);
    }

    public async Task<StreamerSessionSnapshot?> RefreshAsync(
        string expectedStreamer,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            StreamerSessionSnapshot before;
            lock (_stateGate)
                before = _snapshot;
            if (!StringComparer.Ordinal.Equals(before.Streamer, expectedStreamer)) return null;

            try
            {
                var channel = new StreamerSongListChannel(expectedStreamer, before.Config.Streamer.Platform);
                var queueTask = _apiService.FetchQueueSnapshotAsync(channel, cancellationToken);
                var historyTask = _apiService.FetchPlayHistoryAsync(
                    channel,
                    before.Config.SongList.PlayHistoryPeriod,
                    cancellationToken);
                await Task.WhenAll(queueTask, historyTask);
                cancellationToken.ThrowIfCancellationRequested();

                var queue = await queueTask;
                var played = await historyTask;
                StreamerSessionSnapshot updated;
                lock (_stateGate)
                {
                    if (!StringComparer.Ordinal.Equals(_snapshot.Streamer, expectedStreamer)) return null;

                    var latestConfig = _snapshot.Config;
                    updated = _snapshot with
                    {
                        Config = latestConfig,
                        AvailableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, latestConfig)
                            .ToArray(),
                        PlayedSongs = played,
                        NowPlaying = queue.Playing,
                        ApiHealth = StreamerSessionHealth.Healthy,
                        ApiHealthDetail = $"Queue and history last synchronized at {DateTime.Now:t}."
                    };
                    _snapshot = updated;
                }

                await _overlayService.UpdateStateAsync(
                    updated.Config,
                    updated.AvailableSongs.ToList(),
                    updated.PlayedSongs,
                    updated.NowPlaying,
                    updated.Streamer);
                RaiseChanged();
                return updated;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                UpdateApiHealth(StreamerSessionHealth.Failed, ex.Message, "Realtime refresh failed.");
                throw;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task UpdateSnapshotAsync(
        SpinnerConfig config,
        IReadOnlyCollection<SpinnerQueueItem> availableSongs,
        IReadOnlyCollection<PlayHistoryItem> playedSongs,
        SpinnerQueueItem? nowPlaying,
        CancellationToken cancellationToken = default)
    {
        StreamerSessionSnapshot updated;
        lock (_stateGate)
        {
            updated = _snapshot with
            {
                Config = config,
                AvailableSongs = availableSongs.ToArray(),
                PlayedSongs = playedSongs.ToArray(),
                NowPlaying = nowPlaying,
                ApiHealth = StreamerSessionHealth.Healthy,
                ApiHealthDetail = $"Queue and history last synchronized at {DateTime.Now:t}."
            };
            _snapshot = updated;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _overlayService.UpdateStateAsync(
            config,
            availableSongs.ToList(),
            playedSongs.ToArray(),
            nowPlaying,
            updated.Streamer);
        RaiseChanged();
    }

    public async Task UpdateConfigAsync(SpinnerConfig config, CancellationToken cancellationToken = default)
    {
        var hasChannel = false;
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Config = config };
            hasChannel = _snapshot.HasChannel;
        }

        await _overlayService.UpdateConfigAsync(config);
        RaiseChanged();
        if (hasChannel)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _refreshSignals?.Writer.TryWrite(true);
        }
    }

    public void SetRefreshSuspended(bool suspended)
    {
        lock (_stateGate)
            _refreshSuspended = suspended;

        if (!suspended)
            _refreshSignals?.Writer.TryWrite(true);
    }

    public async Task ClearAsync(SpinnerConfig config)
    {
        await StopSubscriptionAsync();
        lock (_stateGate)
        {
            _refreshSuspended = false;
            _snapshot = StreamerSessionSnapshot.Empty with { Config = config };
        }

        await _overlayService.UpdateStateAsync(config, [], [], null, "");
        RaiseChanged();
    }

    private async Task RunEventsAsync(
        int streamerId,
        ChannelWriter<bool> refreshSignals,
        CancellationToken cancellationToken)
    {
        var wasDisconnected = false;
        try
        {
            await foreach (var notification in _eventSource.SubscribeAsync(streamerId, cancellationToken))
            {
                if (notification.Kind == StreamerSongListEventKind.Connected)
                {
                    refreshSignals.TryWrite(true);
                    UpdateRealtimeHealth(
                        StreamerSessionHealth.Healthy,
                        "Receiving live queue and history events.",
                        wasDisconnected ? "Realtime updates reconnected." : null);
                    wasDisconnected = false;
                    continue;
                }

                if (notification.Kind is StreamerSongListEventKind.QueueChanged or
                    StreamerSongListEventKind.PlayHistoryChanged)
                {
                    refreshSignals.TryWrite(true);
                    continue;
                }

                if (notification.Kind == StreamerSongListEventKind.Reconnecting)
                {
                    wasDisconnected = true;
                    UpdateRealtimeHealth(
                        StreamerSessionHealth.Degraded,
                        notification.Error ?? "The event connection was interrupted and is reconnecting.",
                        "Realtime updates disconnected; reconnecting...");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            UpdateRealtimeHealth(
                StreamerSessionHealth.Failed,
                ex.Message,
                $"Realtime updates stopped: {ex.Message}");
            Trace.WriteLine($"[SonglistSpinner] Realtime updates stopped: {ex}");
        }
        finally
        {
            refreshSignals.TryComplete();
        }
    }

    private async Task RunRefreshesAsync(
        string streamer,
        ChannelReader<bool> refreshSignals,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await refreshSignals.WaitToReadAsync(cancellationToken))
            {
                while (refreshSignals.TryRead(out _))
                {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                while (refreshSignals.TryRead(out _))
                {
                }

                bool suspended;
                lock (_stateGate)
                    suspended = _refreshSuspended;
                if (suspended) continue;

                try
                {
                    await RefreshAsync(streamer, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[SonglistSpinner] Realtime refresh failed and will retry on the next event: {ex}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            UpdateApiHealth(StreamerSessionHealth.Failed, ex.Message, $"Realtime refresh failed: {ex.Message}");
            Trace.WriteLine($"[SonglistSpinner] Realtime refresh failed: {ex}");
        }
    }

    private void UpdateApiHealth(StreamerSessionHealth health, string detail, string? announcement)
    {
        lock (_stateGate)
            _snapshot = _snapshot with { ApiHealth = health, ApiHealthDetail = detail };
        RaiseChanged(announcement);
    }

    private void UpdateRealtimeHealth(StreamerSessionHealth health, string detail, string? announcement)
    {
        lock (_stateGate)
            _snapshot = _snapshot with { RealtimeHealth = health, RealtimeHealthDetail = detail };
        RaiseChanged(announcement);
    }

    private void RaiseChanged(string? announcement = null)
    {
        var handlers = Changed;
        if (handlers is null) return;

        var args = new StreamerSessionChangedEventArgs(GetSnapshot(), announcement);
        foreach (EventHandler<StreamerSessionChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SonglistSpinner] A streamer session observer failed: {ex}");
            }
        }
    }

    private async Task StopSubscriptionAsync()
    {
        var cancellationSource = _subscriptionCts;
        var subscriptionTask = _subscriptionTask;
        var refreshTask = _refreshTask;
        var refreshSignals = _refreshSignals;

        _subscriptionCts = null;
        _subscriptionTask = null;
        _refreshTask = null;
        _refreshSignals = null;

        cancellationSource?.Cancel();
        refreshSignals?.Writer.TryComplete();
        var tasks = new[] { subscriptionTask, refreshTask }.Where(task => task is not null).Cast<Task>().ToArray();
        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellationSource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _lifetimeCts.Cancel();
        await StopSubscriptionAsync();
        _refreshGate.Dispose();
        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed record StreamerSessionSnapshot(
    int StreamerId,
    string Streamer,
    SpinnerConfig Config,
    SpinnerQueueItem[] AvailableSongs,
    PlayHistoryItem[] PlayedSongs,
    SpinnerQueueItem? NowPlaying,
    StreamerSessionHealth ApiHealth,
    string ApiHealthDetail,
    StreamerSessionHealth RealtimeHealth,
    string RealtimeHealthDetail)
{
    public bool HasChannel => StreamerId > 0 && !string.IsNullOrWhiteSpace(Streamer);

    public static StreamerSessionSnapshot Empty { get; } = new(
        0,
        "",
        new SpinnerConfig(),
        [],
        [],
        null,
        StreamerSessionHealth.Unknown,
        "Waiting for a channel to be loaded.",
        StreamerSessionHealth.Unknown,
        "Waiting for a channel to be loaded.");
}

public sealed record StreamerSessionChangedEventArgs(
    StreamerSessionSnapshot Snapshot,
    string? Announcement);

public enum StreamerSessionHealth
{
    Unknown,
    Checking,
    Healthy,
    Degraded,
    Failed
}
