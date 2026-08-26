using System.Diagnostics;
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
    private readonly SemaphoreSlim _winnerTransitionGate = new(1, 1);
    private List<SpinnerQueueItem> _availableSongs = [];

    private StreamerSessionHealth _apiHealth = StreamerSessionHealth.Unknown;
    private string _apiHealthDetail = "Waiting for a channel to be loaded.";
    private SpinnerConfig _config = new();
    private string _currentStreamer = "";

    private DotNetObjectReference<Dashboard>? _dotNetRef;
    private bool _channelLoadPending;
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
    private StreamerSessionHealth _realtimeHealth = StreamerSessionHealth.Unknown;
    private string _realtimeHealthDetail = "Waiting for a channel to be loaded.";
    private bool _showStreamerInput = true;
    private string _spinButtonText = "SPIN";

    private bool _spinDisabled;

    private string _status = "";
    private bool _statusVisible;

    private string _streamerInput = "";
    private string? _streamerInputError;
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
    private bool IsSpinDisabled => _spinDisabled || _isSpinning || _markNowPlayingPending || _winnerVisible;
    private string PreferredWinnerActionId => IsNowPlayingWinnerActionEnabled
        ? "setWinnerNowPlayingBtn"
        : _preferMarkWinnerPlayed
            ? "markWinnerPlayedBtn"
            : "leaveWinnerInQueueBtn";
    private string StreamerInput
    {
        get => _streamerInput;
        set
        {
            _streamerInput = value;
            if (_streamerInputError is null) return;

            _streamerInputError = null;
            _status = "";
            _statusVisible = false;
        }
    }

    private string NowPlayingDisplayText => _nowPlaying is null
        ? ""
        : SpinnerDataService.CreateSongTextForFields(
            _nowPlaying,
            _config.NowPlaying?.Fields is { Length: > 0 } fields
                ? fields
                : SongFieldNames.CreateDefaultSelection(),
            _config.NowPlaying?.Separator,
            _config.NowPlaying?.ShowLabels ?? true);
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

        StreamerSession.Changed -= OnStreamerSessionChanged;

        if (_isSpinning)
            SignalSpinCompleted();
        if (_winnerVisible)
            await OverlayService.BroadcastCloseWinnerAsync();

        _lifetimeCts.Cancel();
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

        _winnerTransitionGate.Dispose();
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
            await StreamerSession.UpdateConfigAsync(_config, _lifetimeCts.Token);
            ApplySessionSnapshot(StreamerSession.GetSnapshot());
            StreamerSession.Changed += OnStreamerSessionChanged;

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

        var activeSession = StreamerSession.GetSnapshot();
        if (activeSession.HasChannel)
        {
            ApplySessionSnapshot(activeSession);
            await RebuildWheel(_wheelCts.Token);
            SetStatus($"Live updates remain active for {activeSession.Streamer}.");
        }
        else
        {
            var defaultName = _config.Streamer.DefaultName.Trim();
            if (!string.IsNullOrEmpty(defaultName))
            {
                StreamerInput = defaultName;
                await LoadStreamer();
            }
        }

    }

    private async Task LoadStreamer()
    {
        var name = _streamerInput.Trim();
        _streamerInputError = null;
        if (string.IsNullOrEmpty(name))
        {
            _streamerInputError = "Enter a streamer name before loading.";
            SetStatus("Please enter a streamer name");
            return;
        }

        var previousSession = StreamerSession.GetSnapshot();
        _channelLoadPending = true;
        _currentStreamer = name;
        _showStreamerInput = false;
        SetApiHealth(StreamerSessionHealth.Checking, $"Resolving {name} and loading its queue.");
        SetRealtimeHealth(StreamerSessionHealth.Unknown, "Waiting for the API connection.");
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
            StreamerInput = name;

            await RebuildWheel(_wheelCts.Token);
            SetStatus($"Loaded {_availableSongs.Count} songs. Press SPIN!");
            await StreamerSession.StartAsync(
                streamerId,
                name,
                _config,
                _availableSongs,
                _playedSongs,
                _nowPlaying,
                _lifetimeCts.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ApplySessionSnapshot(previousSession);
            _showStreamerInput = true;
            _streamerInputError =
                $"Could not find or load streamer \"{name}\". Check the name and platform, then try again.";
            SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            _channelLoadPending = false;
        }

        StateHasChanged();
    }

    private async Task Spin()
    {
        if (_isSpinning || _winnerVisible)
        {
            SetStatus("Choose what happens to the current winner before spinning again.");
            return;
        }

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
        StreamerSession.SetRefreshSuspended(true);
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
            var spinDuration = SpinDurationMilliseconds;
            SetStatus("Spinning...");
            await InvokeAsync(StateHasChanged);

            var spinWinner = _availableSongs[winnerIndex];
            var winnerFields = SpinnerDataService.CreateWinnerDialogFields(spinWinner, _config);
            await StreamerSession.UpdateSnapshotAsync(
                _config,
                _availableSongs,
                _playedSongs,
                _nowPlaying,
                _lifetimeCts.Token);
            await OverlayService.BroadcastSpinCommandAsync(
                winnerIndex,
                spinWinner.QueueId,
                spinDuration);

            await JS.InvokeVoidAsync(SpinnerInteropMethods.SpinToItem, winnerIndex, spinDuration);

            await Task.Delay(spinDuration + WinnerRevealDelayMilliseconds, _lifetimeCts.Token);
            var displayedQueuePosition = _config.WinnerDialog.ShowQueuePosition
                ? await ResolveCurrentQueuePositionAsync(
                    spinStreamer,
                    spinWinner.QueueId,
                    _lifetimeCts.Token)
                : null;
            _winnerQueueId = spinWinner.QueueId;
            await ShowWinnerModalAsync(winnerFields, displayedQueuePosition);
            await OverlayService.BroadcastWinnerRevealAsync(winnerFields, displayedQueuePosition);
            SetStatus($"Winner: {SpinnerDataService.BuildWheelLabel(spinWinner)}");
            _spinButtonText = "SPIN";
            _spinDisabled = false;
            StateHasChanged();
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
        await StreamerSession.ClearAsync(_config);
        _showStreamerInput = true;
        _currentStreamer = "";
        StreamerInput = "";
        _streamerId = 0;
        _nowPlaying = null;
        _availableSongs = [];
        _playedSongs = [];
        _winnerVisible = false;
        _winnerQueueId = null;
        _winnerQueuePosition = null;
        SignalSpinCompleted();
        await RebuildWheel(_wheelCts.Token);
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
                SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
                SetStatus($"StreamerSongList failed while marking Now Playing as played: {ex.Message}");
            }
        }
        finally
        {
            _markNowPlayingPending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ShowWinnerModalAsync(WinnerDialogField[] fields, int? queuePosition)
    {
        _winnerFields = fields;
        _winnerQueuePosition = queuePosition;
        _winnerActionError = null;
        _winnerActionPending = false;
        _winnerVisible = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            await JS.InvokeVoidAsync(
                SpinnerInteropMethods.OpenWinnerDialog,
                PreferredWinnerActionId,
                _dotNetRef);
        }
        catch
        {
            _winnerVisible = false;
            await InvokeAsync(StateHasChanged);
            throw;
        }

        _ = JS.InvokeVoidAsync(SpinnerInteropMethods.RunConfetti, (object)_config.WheelColors);
    }

    [JSInvokable]
    public Task OnWinnerDialogCancelled() => LeaveWinnerInQueueAsync();

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
            SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
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
        await InvokeAsync(StateHasChanged);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.CloseWinnerDialog);
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
                StreamerSessionHealth.Healthy,
                $"Queue and history last synchronized at {DateTime.Now:t}.");
            return (await queueTask, await historyTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
            throw;
        }
    }

    private async Task TransitionWinnerToNowPlayingAsync(int queueId, CancellationToken cancellationToken)
    {
        if (_streamerId <= 0)
            throw new InvalidOperationException("The current streamer ID is unavailable. Reload the streamer and try again.");

        var channel = new StreamerSongListChannel(_currentStreamer, _config.Streamer.Platform);
        await _winnerTransitionGate.WaitAsync(cancellationToken);
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
            _winnerTransitionGate.Release();
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

    private async Task RefreshSnapshotAsync(string expectedStreamer, CancellationToken cancellationToken)
    {
        var snapshot = await StreamerSession.RefreshAsync(expectedStreamer, cancellationToken);
        if (snapshot is null) return;

        ApplySessionSnapshot(snapshot);
        await RebuildWheel(_wheelCts.Token);
        StateHasChanged();
    }

    private void OnStreamerSessionChanged(object? sender, StreamerSessionChangedEventArgs e)
    {
        if (_channelLoadPending || _lifetimeCts.IsCancellationRequested) return;

        try
        {
            _ = InvokeAsync(async () =>
            {
                if (_lifetimeCts.IsCancellationRequested || _channelLoadPending) return;

                ApplySessionSnapshot(e.Snapshot);
                if (_jsInitialized && !_isSpinning)
                    await RebuildWheel(_wheelCts.Token);
                if (!string.IsNullOrWhiteSpace(e.Announcement))
                    SetStatus(e.Announcement);
                StateHasChanged();
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ApplySessionSnapshot(StreamerSessionSnapshot snapshot)
    {
        _apiHealth = snapshot.ApiHealth;
        _apiHealthDetail = snapshot.ApiHealthDetail;
        _realtimeHealth = snapshot.RealtimeHealth;
        _realtimeHealthDetail = snapshot.RealtimeHealthDetail;
        _streamerId = snapshot.StreamerId;
        _currentStreamer = snapshot.Streamer;
        _availableSongs = snapshot.AvailableSongs.ToList();
        _playedSongs = snapshot.PlayedSongs;
        _nowPlaying = snapshot.NowPlaying;

        if (!snapshot.HasChannel) return;

        _config = snapshot.Config;
        StreamerInput = snapshot.Streamer;
        _showStreamerInput = false;
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

    private void SetApiHealth(StreamerSessionHealth health, string detail)
    {
        _apiHealth = health;
        _apiHealthDetail = detail;
    }

    private void SignalSpinCompleted()
    {
        _isSpinning = false;
        StreamerSession.SetRefreshSuspended(false);
        _spinCompletion?.TrySetResult(true);
        _spinCompletion = null;
    }

    private void SetRealtimeHealth(StreamerSessionHealth health, string detail)
    {
        _realtimeHealth = health;
        _realtimeHealthDetail = detail;
    }

    private static string ServiceHealthClass(StreamerSessionHealth health) => health switch
    {
        StreamerSessionHealth.Healthy => "healthy",
        StreamerSessionHealth.Checking => "checking",
        StreamerSessionHealth.Degraded => "degraded",
        StreamerSessionHealth.Failed => "failed",
        _ => "unknown"
    };

    private static string ServiceHealthLabel(StreamerSessionHealth health) => health switch
    {
        StreamerSessionHealth.Healthy => "Connected",
        StreamerSessionHealth.Checking => "Checking",
        StreamerSessionHealth.Degraded => "Reconnecting",
        StreamerSessionHealth.Failed => "Error",
        _ => "Not connected"
    };

    private (string label, string cssClass) GetApiEnvironment()
    {
        var host = ApiOptions.BaseAddress.Host;
        if (host.Contains("staging", StringComparison.OrdinalIgnoreCase)) return ("Staging", "staging");
        if (host.Equals("api.streamersonglist.com", StringComparison.OrdinalIgnoreCase)) return ("Production", "production");
        return ("Custom API", "custom");
    }

}
