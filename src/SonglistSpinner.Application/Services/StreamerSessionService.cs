using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private static readonly TimeSpan InitialRefreshRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRefreshRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Events often arrive in bursts, so a refresh waits this long and then covers the whole burst.</summary>
    private static readonly TimeSpan RefreshDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly ISpinnerApiService _apiService;
    private readonly IStreamerSongListEventSource _eventSource;
    private readonly OverlayStateService _overlayService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StreamerSessionService> _logger;
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
        OverlayStateService overlayService,
        TimeProvider? timeProvider = null,
        ILogger<StreamerSessionService>? logger = null)
    {
        _apiService = apiService;
        _eventSource = eventSource;
        _overlayService = overlayService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<StreamerSessionService>.Instance;
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
                DescribeSynchronizedNow(),
                StreamerSessionHealth.Checking,
                $"Connecting to realtime updates for {streamer}.");
        }

        _overlayService.UpdateState(
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

    /// <summary>
    /// Fetches the queue and history for <paramref name="expectedStreamer"/> and publishes them.
    /// Returns <see langword="null"/> without publishing when another channel is loaded, or when
    /// refresh is suspended before the fetch starts or before its result is committed, so a refresh
    /// never replaces the songs under an active spin.
    /// </summary>
    public async Task<StreamerSessionSnapshot?> RefreshAsync(
        string expectedStreamer,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            StreamerSessionSnapshot before;
            lock (_stateGate)
            {
                if (_refreshSuspended) return null;
                before = _snapshot;
            }

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
                    if (_refreshSuspended ||
                        !StringComparer.Ordinal.Equals(_snapshot.Streamer, expectedStreamer))
                        return null;

                    var latestConfig = _snapshot.Config;
                    updated = _snapshot with
                    {
                        Config = latestConfig,
                        AvailableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, latestConfig)
                            .ToArray(),
                        PlayedSongs = played,
                        NowPlaying = queue.Playing,
                        ApiHealth = StreamerSessionHealth.Healthy,
                        ApiHealthDetail = DescribeSynchronizedNow()
                    };
                    _snapshot = updated;
                }

                _overlayService.UpdateState(
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

    public void UpdateSnapshot(
        SpinnerConfig config,
        IReadOnlyCollection<SpinnerQueueItem> availableSongs,
        IReadOnlyCollection<PlayHistoryItem> playedSongs,
        SpinnerQueueItem? nowPlaying)
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
                ApiHealthDetail = DescribeSynchronizedNow()
            };
            _snapshot = updated;
        }

        _overlayService.UpdateState(
            config,
            availableSongs.ToList(),
            playedSongs.ToArray(),
            nowPlaying,
            updated.Streamer);
        RaiseChanged();
    }

    public void UpdateConfig(SpinnerConfig config)
    {
        var hasChannel = false;
        lock (_stateGate)
        {
            _snapshot = _snapshot with { Config = config };
            hasChannel = _snapshot.HasChannel;
        }

        _overlayService.UpdateConfig(config);
        RaiseChanged();
        if (hasChannel)
            _refreshSignals?.Writer.TryWrite(true);
    }

    /// <summary>
    /// Suspends refreshes while a spin owns the wheel. A refresh skipped or discarded while suspended
    /// is requested again on resume. <see cref="UpdateSnapshot"/> still publishes while suspended.
    /// </summary>
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

        _overlayService.UpdateState(config, [], [], null, "");
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
            _logger.LogError(ex, "Realtime updates for streamer {StreamerId} stopped", streamerId);
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

                await Task.Delay(RefreshDebounceDelay, _timeProvider, cancellationToken);
                while (refreshSignals.TryRead(out _))
                {
                }

                await RefreshUntilSucceededAsync(streamer, refreshSignals, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            UpdateApiHealth(StreamerSessionHealth.Failed, ex.Message, $"Realtime refresh failed: {ex.Message}");
            _logger.LogError(ex, "Realtime refreshes for {Streamer} stopped", streamer);
        }
    }

    /// <summary>
    /// Retries a failed realtime refresh with exponential backoff instead of waiting for the next
    /// event, which may never come. Stops retrying while refresh is suspended, because resuming
    /// requests a fresh refresh.
    /// </summary>
    private async Task RefreshUntilSucceededAsync(
        string streamer,
        ChannelReader<bool> refreshSignals,
        CancellationToken cancellationToken)
    {
        var retryDelay = InitialRefreshRetryDelay;
        var failed = false;
        while (!IsRefreshSuspended())
        {
            try
            {
                await RefreshAsync(streamer, cancellationToken);
                if (failed)
                {
                    _logger.LogInformation("Realtime refresh for {Streamer} recovered", streamer);
                    RaiseChanged("Realtime refresh recovered.");
                }

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Retries never give up, so the first failure is the degradation worth a warning and the
                // attempts after it are retry detail.
                _logger.Log(
                    failed ? LogLevel.Debug : LogLevel.Warning,
                    ex,
                    "Realtime refresh for {Streamer} failed; retrying in {RetryDelay}",
                    streamer,
                    retryDelay);
                failed = true;
            }

            await Task.Delay(retryDelay, _timeProvider, cancellationToken);
            // The retry fetches the latest state, so signals that arrived while waiting are already covered.
            while (refreshSignals.TryRead(out _))
            {
            }

            retryDelay = retryDelay * 2 < MaxRefreshRetryDelay ? retryDelay * 2 : MaxRefreshRetryDelay;
        }
    }

    private string DescribeSynchronizedNow() =>
        $"Queue and history last synchronized at {_timeProvider.GetLocalNow():t}.";

    private bool IsRefreshSuspended()
    {
        lock (_stateGate)
            return _refreshSuspended;
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
                _logger.LogError(ex, "A streamer session observer failed");
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
