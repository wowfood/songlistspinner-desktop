using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

// Injected properties are generated from @inject directives in Dashboard.razor.
public partial class Dashboard
{
    private const int SpinDurationMilliseconds = 5000;
    private const int WinnerQueuePositionLookupTimeoutMilliseconds = 2000;
    private const int WinnerRevealDelayMilliseconds = 100;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private List<SpinnerQueueItem> _availableSongs = [];

    private DashboardServiceHealth _apiHealth = DashboardServiceHealth.Unknown;
    private string _apiHealthDetail = "Waiting for a channel to be loaded.";
    private SpinnerConfig _config = new();
    private string _currentStreamer = "";

    private DotNetObjectReference<Dashboard>? _dotNetRef;
    private Task? _eventRefreshTask;
    private Channel<bool>? _eventRefreshSignals;
    private CancellationTokenSource? _eventSubscriptionCts;
    private Task? _eventSubscriptionTask;
    private bool _isLockedDefault;
    private bool _isSpinning;
    private TaskCompletionSource<bool>? _spinCompletion;
    private bool _jsInitialized;
    private DateTime _lastSpinTime = DateTime.MinValue;
    private bool _loading = true;
    private bool _markNowPlayingPending;
    private SpinnerQueueItem? _nowPlaying;
    private LocalOverlayHealth _overlayHealth = new(LocalOverlayServerState.Stopped, 0, null);
    private bool _overlayHealthSubscribed;
    private bool _playedListCollapsed;
    private CancellationTokenSource? _playedRefreshCts;
    private PlayHistoryItem[] _playedSongs = [];
    private DashboardServiceHealth _realtimeHealth = DashboardServiceHealth.Unknown;
    private string _realtimeHealthDetail = "Waiting for a channel to be loaded.";
    private bool _showStreamerInput = true;
    private string _spinButtonText = "SPIN";

    private bool _spinDisabled;

    private string _status = "";
    private bool _statusVisible;

    private string _streamerInput = "";
    private int _streamerId;
    private CancellationTokenSource _wheelCts = new();

    private bool _wheelVisible = true;
    private WinnerDialogField[] _winnerFields = [];
    private string? _winnerActionError;
    private bool _winnerActionPending;
    private int? _winnerQueueId;
    private int? _winnerQueuePosition;
    private bool _winnerVisible;
    private bool _preferMarkWinnerPlayed;

    private bool IsNowPlayingWinnerActionEnabled => _config.NowPlaying?.Enabled == true;
    private string NowPlayingDisplayText => _nowPlaying is null
        ? ""
        : SpinnerDataService.CreateSongTextForFields(
            _nowPlaying,
            _config.NowPlaying?.Fields is { Length: > 0 } fields
                ? fields
                : SongFieldNames.CreateDefaultSelection());
    private string ApiEnvironmentLabel => GetApiEnvironment().label;
    private string ApiEnvironmentClass => GetApiEnvironment().cssClass;
    private string OverlayHealthClass => _overlayHealth.ServerState switch
    {
        LocalOverlayServerState.Running when _overlayHealth.ConnectedClients > 0 => "healthy",
        LocalOverlayServerState.Running => "healthy",
        LocalOverlayServerState.Starting => "checking",
        LocalOverlayServerState.Failed => "failed",
        _ => "unknown"
    };

    private string OverlayHealthLabel => _overlayHealth.ServerState switch
    {
        LocalOverlayServerState.Running when _overlayHealth.ConnectedClients == 1 => "1 connected",
        LocalOverlayServerState.Running when _overlayHealth.ConnectedClients > 1 =>
            $"{_overlayHealth.ConnectedClients} connected",
        LocalOverlayServerState.Running => "Ready",
        LocalOverlayServerState.Starting => "Starting",
        LocalOverlayServerState.Failed => "Error",
        _ => "Stopped"
    };

    private string OverlayHealthDetail => _overlayHealth.ServerState switch
    {
        LocalOverlayServerState.Running =>
            $"{OverlayService.OverlayUrl} — {_overlayHealth.ConnectedClients} connected browser source(s).",
        LocalOverlayServerState.Failed => _overlayHealth.Error ?? "The local overlay server failed.",
        LocalOverlayServerState.Starting => "The local OBS overlay server is starting.",
        _ => "The local OBS overlay server is stopped."
    };

    public async ValueTask DisposeAsync()
    {
        if (_overlayHealthSubscribed)
        {
            OverlayService.HealthChanged -= OnOverlayHealthChanged;
            _overlayHealthSubscribed = false;
        }

        _lifetimeCts.Cancel();
        await StopRealtimeUpdatesAsync();
        _playedRefreshCts?.Cancel();
        _playedRefreshCts?.Dispose();
        _wheelCts.Cancel();
        _wheelCts.Dispose();
        try
        {
            await JS.InvokeVoidAsync(SpinnerInteropMethods.DisposeDashboardBindings);
        }
        catch (Exception ex) { _ = ex; }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        try
        {
            await JS.InvokeVoidAsync("document.body.classList.remove", "spinner-page");
        }
        catch (Exception ex) { _ = ex; }

        try
        {
            await JS.InvokeVoidAsync(SpinnerInteropMethods.ResetBackground);
        }
        catch (Exception ex) { _ = ex; }

        _refreshGate.Dispose();
        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            StreamerSongListCredential? credential;
            try
            {
                credential = await CredentialStore.GetCredentialAsync(_lifetimeCts.Token);
            }
            catch
            {
                credential = null;
            }

            if (credential is null)
            {
                Navigation.NavigateTo("/setup", replace: true);
                return;
            }

            _overlayHealth = OverlayService.GetHealth();
            OverlayService.HealthChanged += OnOverlayHealthChanged;
            _overlayHealthSubscribed = true;
            await JS.InvokeVoidAsync("document.body.classList.add", "spinner-page");
            var settings = LocalSettings.LoadSettings();
            _config = LocalSettings.ToSpinnerConfig(settings);
            _preferMarkWinnerPlayed = settings.UpdateQueueAfterSpin && !settings.DisplayNowPlaying;
            _isLockedDefault = _config.Streamer.HideChangeOptionWhenDefault
                               && !string.IsNullOrWhiteSpace(_config.Streamer.DefaultName);

            _loading = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_jsInitialized) return;
        _jsInitialized = true;

        await JS.InvokeVoidAsync(
            SpinnerInteropMethods.ApplyTheme, _config.Colors, _config.PlayedList, _config.WinnerDialog);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.ApplyBackground, _config.Background);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.ApplyPlayedListPosition,
            _config.SongList.PlayedListPosition);

        await JS.InvokeVoidAsync(SpinnerInteropMethods.CreateWheel,
            new[] { new { label = "Enter streamer name above" } },
            _config.WheelColors);

        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetupResizeObserver);
        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetupResizeHandlers, _dotNetRef);

        var defaultName = _config.Streamer.DefaultName.Trim();
        if (!string.IsNullOrEmpty(defaultName))
        {
            _streamerInput = defaultName;
            await LoadStreamer();
        }

    }

    private async Task LoadStreamer()
    {
        var name = _streamerInput.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Please enter a streamer name");
            return;
        }

        await StopRealtimeUpdatesAsync();
        _currentStreamer = name;
        _showStreamerInput = false;
        SetApiHealth(DashboardServiceHealth.Checking, $"Resolving {name} and loading its queue.");
        SetRealtimeHealth(DashboardServiceHealth.Unknown, "Waiting for the API connection.");
        SetStatus("Loading songs...");
        StateHasChanged();

        try
        {
            var channel = new StreamerSongListChannel(name, _config.Streamer.Platform);
            var streamerId = await ApiService.ResolveStreamerIdAsync(channel, _lifetimeCts.Token);
            var (queue, played) = await FetchQueueAndHistory(name, _lifetimeCts.Token);
            _streamerId = streamerId;
            _nowPlaying = queue.Playing;
            _playedSongs = played;
            _availableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, _config);

            await RebuildWheel(_wheelCts.Token);
            SetStatus($"Loaded {_availableSongs.Count} songs. Press SPIN!");
            await OverlayService.UpdateStateAsync(
                _config, _availableSongs, _playedSongs, _nowPlaying, _currentStreamer);
            StartRealtimeUpdates(streamerId, name);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetApiHealth(DashboardServiceHealth.Failed, ex.Message);
            SetStatus($"Error: {ex.Message}");
        }

        StateHasChanged();
    }

    private async Task Spin()
    {
        if (_markNowPlayingPending)
        {
            SetStatus("Wait for the Now Playing update to finish.");
            return;
        }

        if (string.IsNullOrEmpty(_currentStreamer))
        {
            SetStatus("Please enter a streamer name first");
            return;
        }

        if ((DateTime.UtcNow - _lastSpinTime).TotalMilliseconds < 1000)
        {
            SetStatus("Cooldown active");
            return;
        }

        var spinStreamer = _currentStreamer;
        _lastSpinTime = DateTime.UtcNow;
        _spinDisabled = true;
        _isSpinning = true;
        _spinCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _winnerQueueId = null;
        _winnerQueuePosition = null;
        _wheelCts.Cancel();
        _wheelCts = new CancellationTokenSource();
        SetStatus("Fetching queue...");
        StateHasChanged();

        try
        {
            var (queue, played) = await FetchQueueAndHistory(spinStreamer, _lifetimeCts.Token);
            _nowPlaying = queue.Playing;
            _playedSongs = played;
            _availableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, _config);

            if (_availableSongs.Count == 0)
            {
                SetStatus("No songs left to spin!");
                _spinDisabled = false;
                SignalSpinCompleted();
                await InvokeAsync(StateHasChanged);
                return;
            }

            await RebuildWheel(_wheelCts.Token);
            var winnerIndex = Random.Shared.Next(_availableSongs.Count);
            SetStatus("Spinning...");
            await InvokeAsync(StateHasChanged);

            var spinWinner = _availableSongs[winnerIndex];
            var winnerFields = SpinnerDataService.CreateWinnerDialogFields(spinWinner, _config);
            await OverlayService.UpdateStateAsync(
                _config, _availableSongs, _playedSongs, _nowPlaying, _currentStreamer);
            await OverlayService.BroadcastSpinCommandAsync(
                winnerIndex,
                spinWinner.QueueId,
                SpinDurationMilliseconds);

            await JS.InvokeVoidAsync(SpinnerInteropMethods.SpinToItem, winnerIndex, SpinDurationMilliseconds);

            await Task.Delay(SpinDurationMilliseconds + WinnerRevealDelayMilliseconds, _lifetimeCts.Token);
            var displayedQueuePosition = _config.WinnerDialog.ShowQueuePosition
                ? await ResolveCurrentQueuePositionAsync(
                    spinStreamer,
                    spinWinner.QueueId,
                    _lifetimeCts.Token)
                : null;
            _winnerQueueId = spinWinner.QueueId;
            ShowWinnerModal(winnerFields, displayedQueuePosition);
            await OverlayService.BroadcastWinnerRevealAsync(winnerFields, displayedQueuePosition);
            SetStatus($"Winner: {SpinnerDataService.BuildWheelLabel(spinWinner)}");
            StateHasChanged();

            for (var i = 1; i >= 0; i--)
            {
                await Task.Delay(1000, _lifetimeCts.Token);
                _spinButtonText = i > 0 ? $"{i}" : "SPIN";
                if (i == 0) _spinDisabled = false;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            _spinDisabled = false;
            SignalSpinCompleted();
            StateHasChanged();
        }
    }

    private async Task ChangeStreamer()
    {
        await StopRealtimeUpdatesAsync();
        _showStreamerInput = true;
        _currentStreamer = "";
        _streamerInput = "";
        _streamerId = 0;
        _nowPlaying = null;
        _availableSongs = [];
        _playedSongs = [];
        _winnerVisible = false;
        _winnerQueueId = null;
        _winnerQueuePosition = null;
        SignalSpinCompleted();
        await RebuildWheel(_wheelCts.Token);
        await OverlayService.UpdateStateAsync(_config, _availableSongs, _playedSongs, null, "");
        StateHasChanged();
    }

    private async Task OnWheelToggle(ChangeEventArgs e)
    {
        _wheelVisible = (bool)(e.Value ?? true);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetWheelVisible, _wheelVisible);
        _ = OverlayService.BroadcastWheelVisibilityAsync(_wheelVisible);
    }

    private async Task ToggleCollapse()
    {
        _playedListCollapsed = !_playedListCollapsed;
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetPlayedListCollapsed,
            _playedListCollapsed, _config.SongList.PlayedListPosition);
        await OverlayService.UpdatePlayedListCollapsedAsync(_playedListCollapsed);
    }

    private async Task MarkNowPlayingPlayedAsync()
    {
        if (_markNowPlayingPending || _isSpinning || _nowPlaying is null) return;
        if (_streamerId <= 0 || string.IsNullOrWhiteSpace(_currentStreamer))
        {
            SetStatus("The current streamer is unavailable. Reload the streamer and try again.");
            return;
        }

        var streamerId = _streamerId;
        var streamer = _currentStreamer;
        var markedPlayed = false;
        _markNowPlayingPending = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await ApiService.MarkNowPlayingAsPlayedAsync(streamerId, _lifetimeCts.Token);
            markedPlayed = true;
            SetStatus("Now Playing marked as played.");
            await RefreshSnapshotAsync(streamer, _lifetimeCts.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (markedPlayed)
            {
                SetStatus($"Now Playing was marked as played, but the dashboard refresh failed: {ex.Message}");
                Trace.WriteLine($"[SonglistSpinner] Refresh after marking Now Playing failed: {ex}");
            }
            else
            {
                SetApiHealth(DashboardServiceHealth.Failed, ex.Message);
                SetStatus($"StreamerSongList failed while marking Now Playing as played: {ex.Message}");
            }
        }
        finally
        {
            _markNowPlayingPending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ShowWinnerModal(WinnerDialogField[] fields, int? queuePosition)
    {
        _winnerFields = fields;
        _winnerQueuePosition = queuePosition;
        _winnerActionError = null;
        _winnerActionPending = false;
        _winnerVisible = true;
        _ = JS.InvokeVoidAsync(SpinnerInteropMethods.RunConfetti, (object)_config.WheelColors);
    }

    private Task MarkWinnerPlayedAsync()
    {
        return ExecuteWinnerActionAsync(
            "marking the winner played",
            "Winner marked as played.",
            (queueId, cancellationToken) => ApiService.MarkQueueItemAsPlayedAsync(queueId, cancellationToken));
    }

    private Task SetWinnerNowPlayingAsync()
    {
        return ExecuteWinnerActionAsync(
            "updating Now Playing",
            "Winner promoted to Now Playing.",
            TransitionWinnerToNowPlayingAsync);
    }

    private async Task LeaveWinnerInQueueAsync()
    {
        if (_winnerActionPending || !_winnerVisible) return;

        _winnerActionPending = true;
        _winnerActionError = null;
        try
        {
            await CompleteWinnerActionAsync("Winner left in the queue.");
        }
        finally
        {
            _winnerActionPending = false;
        }
    }

    private async Task ExecuteWinnerActionAsync(
        string actionDescription,
        string successMessage,
        Func<int, CancellationToken, Task> action)
    {
        if (_winnerActionPending || !_winnerVisible) return;
        if (_winnerQueueId is not { } queueId)
        {
            _winnerActionError = "The selected queue entry is unavailable. Leave it in the queue and spin again.";
            SetStatus(_winnerActionError);
            return;
        }

        _winnerActionPending = true;
        _winnerActionError = null;
        try
        {
            await action(queueId, _lifetimeCts.Token);
            await CompleteWinnerActionAsync(successMessage);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetApiHealth(DashboardServiceHealth.Failed, ex.Message);
            _winnerActionError = $"StreamerSongList failed while {actionDescription}: {ex.Message}";
            SetStatus(_winnerActionError);
        }
        finally
        {
            _winnerActionPending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CompleteWinnerActionAsync(string statusMessage)
    {
        _winnerVisible = false;
        SignalSpinCompleted();
        await OverlayService.BroadcastCloseWinnerAsync();
        SetStatus(statusMessage);

        _winnerQueueId = null;
        _winnerQueuePosition = null;
        _playedRefreshCts?.Cancel();
        _playedRefreshCts = new CancellationTokenSource();
        await RefreshAfterWinnerAsync(_playedRefreshCts.Token);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnResizeEnd(string width, string minWidth) =>
        OverlayService.UpdatePlayedListWidthAsync(width, minWidth);

    private async Task<int?> ResolveCurrentQueuePositionAsync(
        string expectedStreamer,
        int queueId,
        CancellationToken cancellationToken)
    {
        if (queueId <= 0 ||
            !string.Equals(_currentStreamer, expectedStreamer, StringComparison.Ordinal))
            return null;

        try
        {
            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lookupCts.CancelAfter(WinnerQueuePositionLookupTimeoutMilliseconds);
            var channel = new StreamerSongListChannel(expectedStreamer, _config.Streamer.Platform);
            var queue = await ApiService.FetchQueueSnapshotAsync(channel, lookupCts.Token);
            if (!string.Equals(_currentStreamer, expectedStreamer, StringComparison.Ordinal)) return null;
            return SpinnerDataService.FindQueuePosition(queue.Items, queueId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Trace.WriteLine("[SonglistSpinner] Timed out while refreshing the winner queue position.");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SonglistSpinner] Could not refresh the winner queue position: {ex}");
            return null;
        }
    }

    private async Task<(SpinnerQueueSnapshot queue, PlayHistoryItem[] played)> FetchQueueAndHistory(
        string streamer,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = _config.SongList.PlayHistoryPeriod;
            var channel = new StreamerSongListChannel(streamer, _config.Streamer.Platform);
            var queueTask = ApiService.FetchQueueSnapshotAsync(channel, cancellationToken);
            var historyTask = ApiService.FetchPlayHistoryAsync(channel, period, cancellationToken);
            await Task.WhenAll(queueTask, historyTask);
            SetApiHealth(
                DashboardServiceHealth.Healthy,
                $"Queue and history last synchronized at {DateTime.Now:t}.");
            return (await queueTask, await historyTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            SetApiHealth(DashboardServiceHealth.Failed, ex.Message);
            throw;
        }
    }

    private async Task TransitionWinnerToNowPlayingAsync(int queueId, CancellationToken cancellationToken)
    {
        if (_streamerId <= 0)
            throw new InvalidOperationException("The current streamer ID is unavailable. Reload the streamer and try again.");

        var channel = new StreamerSongListChannel(_currentStreamer, _config.Streamer.Platform);
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await NowPlayingTransitions.PromoteWinnerAsync(
                channel,
                _streamerId,
                queueId,
                cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RebuildWheel(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return;
        var items = _availableSongs.Count > 0
            ? _availableSongs.Select(s => new { label = SpinnerDataService.BuildWheelLabel(s) }).ToArray<object>()
            : new object[] { new { label = "No songs in queue" } };
        await JS.InvokeVoidAsync(SpinnerInteropMethods.CreateWheel, ct, items, _config.WheelColors);
    }

    private void SetStatus(string message, bool visible = true)
    {
        _status = message;
        _statusVisible = visible && !string.IsNullOrWhiteSpace(message);
        Trace.WriteLine($"[SonglistSpinner] {message}");
    }

    private async Task OnStreamerKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await LoadStreamer();
    }

    private async Task RefreshAfterWinnerAsync(CancellationToken ct)
    {
        try
        {
            if (_isSpinning || string.IsNullOrEmpty(_currentStreamer)) return;
            await RefreshSnapshotAsync(_currentStreamer, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus($"Post-spin refresh failed: {ex.Message}");
            Trace.WriteLine($"[SonglistSpinner] Post-spin refresh failed: {ex}");
            await InvokeAsync(StateHasChanged);
        }
    }

    private void StartRealtimeUpdates(int streamerId, string streamer)
    {
        SetRealtimeHealth(DashboardServiceHealth.Checking, $"Connecting to realtime updates for {streamer}.");
        _eventSubscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _eventRefreshSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var cancellationToken = _eventSubscriptionCts.Token;
        _eventSubscriptionTask = RunRealtimeEventsAsync(
            streamerId,
            _eventRefreshSignals.Writer,
            cancellationToken);
        _eventRefreshTask = RunRealtimeRefreshAsync(
            streamer,
            _eventRefreshSignals.Reader,
            cancellationToken);
    }

    private async Task StopRealtimeUpdatesAsync()
    {
        var cancellationSource = _eventSubscriptionCts;
        var subscriptionTask = _eventSubscriptionTask;
        var refreshTask = _eventRefreshTask;
        var refreshSignals = _eventRefreshSignals;

        _eventSubscriptionCts = null;
        _eventSubscriptionTask = null;
        _eventRefreshTask = null;
        _eventRefreshSignals = null;

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
        SetRealtimeHealth(
            DashboardServiceHealth.Unknown,
            string.IsNullOrWhiteSpace(_currentStreamer)
                ? "Waiting for a channel to be loaded."
                : "Realtime updates are not connected.");
    }

    private async Task RunRealtimeEventsAsync(
        int streamerId,
        ChannelWriter<bool> refreshSignals,
        CancellationToken cancellationToken)
    {
        var wasDisconnected = false;
        try
        {
            await foreach (var notification in EventSource.SubscribeAsync(streamerId, cancellationToken))
            {
                if (notification.Kind == StreamerSongListEventKind.Connected)
                {
                    refreshSignals.TryWrite(true);
                    await InvokeAsync(() =>
                    {
                        SetRealtimeHealth(DashboardServiceHealth.Healthy, "Receiving live queue and history events.");
                        StateHasChanged();
                    });
                    if (wasDisconnected)
                    {
                        wasDisconnected = false;
                        await InvokeAsync(() =>
                        {
                            SetStatus("Realtime updates reconnected.");
                            StateHasChanged();
                        });
                    }

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
                    await InvokeAsync(() =>
                    {
                        SetRealtimeHealth(
                            DashboardServiceHealth.Degraded,
                            notification.Error ?? "The event connection was interrupted and is reconnecting.");
                        SetStatus("Realtime updates disconnected; reconnecting...");
                        StateHasChanged();
                    });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                SetRealtimeHealth(DashboardServiceHealth.Failed, ex.Message);
                SetStatus($"Realtime updates stopped: {ex.Message}");
                Trace.WriteLine($"[SonglistSpinner] Realtime updates stopped: {ex}");
                StateHasChanged();
            });
        }
        finally
        {
            refreshSignals.TryComplete();
        }
    }

    private async Task RunRealtimeRefreshAsync(
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

                Task? spinCompletion = null;
                await InvokeAsync(() =>
                {
                    if (_isSpinning) spinCompletion = _spinCompletion?.Task;
                });
                if (spinCompletion is not null)
                    await spinCompletion.WaitAsync(cancellationToken);

                await RefreshSnapshotAsync(streamer, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                SetApiHealth(DashboardServiceHealth.Failed, ex.Message);
                SetStatus($"Realtime refresh failed: {ex.Message}");
                Trace.WriteLine($"[SonglistSpinner] Realtime refresh failed: {ex}");
                StateHasChanged();
            });
        }
    }

    private async Task RefreshSnapshotAsync(string expectedStreamer, CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var isCurrentStreamer = false;
            await InvokeAsync(() =>
            {
                isCurrentStreamer = string.Equals(_currentStreamer, expectedStreamer, StringComparison.Ordinal);
            });
            if (!isCurrentStreamer) return;

            var (queue, played) = await FetchQueueAndHistory(expectedStreamer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await InvokeAsync(async () =>
            {
                if (!string.Equals(_currentStreamer, expectedStreamer, StringComparison.Ordinal)) return;

                _nowPlaying = queue.Playing;
                _playedSongs = played;
                _availableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, _config);
                await RebuildWheel(_wheelCts.Token);
                await OverlayService.UpdateStateAsync(
                    _config, _availableSongs, _playedSongs, _nowPlaying, _currentStreamer);
                StateHasChanged();
            });
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void OnOverlayHealthChanged(object? sender, EventArgs e)
    {
        var health = OverlayService.GetHealth();
        try
        {
            _ = InvokeAsync(() =>
            {
                _overlayHealth = health;
                StateHasChanged();
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void SetApiHealth(DashboardServiceHealth health, string detail)
    {
        _apiHealth = health;
        _apiHealthDetail = detail;
    }

    private void SignalSpinCompleted()
    {
        _isSpinning = false;
        _spinCompletion?.TrySetResult(true);
        _spinCompletion = null;
    }

    private void SetRealtimeHealth(DashboardServiceHealth health, string detail)
    {
        _realtimeHealth = health;
        _realtimeHealthDetail = detail;
    }

    private static string ServiceHealthClass(DashboardServiceHealth health) => health switch
    {
        DashboardServiceHealth.Healthy => "healthy",
        DashboardServiceHealth.Checking => "checking",
        DashboardServiceHealth.Degraded => "degraded",
        DashboardServiceHealth.Failed => "failed",
        _ => "unknown"
    };

    private static string ServiceHealthLabel(DashboardServiceHealth health) => health switch
    {
        DashboardServiceHealth.Healthy => "Connected",
        DashboardServiceHealth.Checking => "Checking",
        DashboardServiceHealth.Degraded => "Reconnecting",
        DashboardServiceHealth.Failed => "Error",
        _ => "Not connected"
    };

    private (string label, string cssClass) GetApiEnvironment()
    {
        var host = ApiOptions.BaseAddress.Host;
        if (host.Contains("staging", StringComparison.OrdinalIgnoreCase)) return ("Staging", "staging");
        if (host.Equals("api.streamersonglist.com", StringComparison.OrdinalIgnoreCase)) return ("Production", "production");
        return ("Custom API", "custom");
    }

    private enum DashboardServiceHealth
    {
        Unknown,
        Checking,
        Healthy,
        Degraded,
        Failed
    }
}
