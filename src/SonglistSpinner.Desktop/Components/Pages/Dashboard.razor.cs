using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

// Injected properties are generated from @inject directives in Dashboard.razor.
public partial class Dashboard
{
    private const string SpinButtonText = "SPIN";
    private readonly CancellationTokenSource _lifetimeCts = new();
    private IReadOnlyList<SpinnerQueueItem> _availableSongs = [];

    private StreamerSessionHealth _apiHealth = StreamerSessionHealth.Unknown;
    private string _apiHealthDetail = "Waiting for a channel to be loaded.";
    private SpinnerConfig _config = new();
    private string _currentStreamer = "";

    private DotNetObjectReference<Dashboard>? _dotNetRef;
    private DashboardActivity _activity = DashboardActivity.Idle;
    private bool _isLockedDefault;
    private bool _jsInitialized;
    private bool _loading = true;
    private SpinnerQueueItem? _nowPlaying;
    private LocalOverlayHealth _overlayHealth = new(LocalOverlayServerState.Stopped, 0, null);
    private bool _overlayHealthSubscribed;
    private bool _playedListCollapsed;
    private CancellationTokenSource? _playedRefreshCts;
    private PlayHistoryItem[] _playedSongs = [];
    private StreamerSessionHealth _realtimeHealth = StreamerSessionHealth.Unknown;
    private string _realtimeHealthDetail = "Waiting for a channel to be loaded.";
    private bool _showStreamerInput = true;

    private string _status = "";
    private bool _statusVisible;

    private string _streamerInput = "";
    private string? _streamerInputError;
    private int _streamerId;
    private CancellationTokenSource _wheelCts = new();

    private bool _wheelVisible = true;
    private WinnerDialogField[] _winnerFields = [];
    private string? _winnerActionError;

    // Set from a winner action's start until its follow-up refresh ends. The spin, and so the winner,
    // finishes before that refresh, so this outlives ShowingWinner and the dialog stays busy while it closes.
    private bool _winnerActionPending;
    private int? _winnerQueueId;
    private int? _winnerQueuePosition;
    private bool _preferMarkWinnerPlayed;

    private bool IsNowPlayingWinnerActionEnabled => _config.NowPlaying?.Enabled == true;
    private bool IsBusy => _activity != DashboardActivity.Idle;
    private bool IsSpinInProgress => _activity is DashboardActivity.Spinning or DashboardActivity.ShowingWinner;
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
        : SpinnerDataService.CreateNowPlayingText(_nowPlaying, _config.NowPlaying);
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
            $"{OverlayServer.OverlayUrl} — {_overlayHealth.ConnectedClients} connected browser source(s).",
        LocalOverlayServerState.Failed => _overlayHealth.Error ?? "The local overlay server failed.",
        LocalOverlayServerState.Starting => "The local OBS overlay server is starting.",
        _ => "The local OBS overlay server is stopped."
    };

    public async ValueTask DisposeAsync()
    {
        if (_overlayHealthSubscribed)
        {
            OverlayServer.HealthChanged -= OnOverlayHealthChanged;
            _overlayHealthSubscribed = false;
        }

        StreamerSession.Changed -= OnStreamerSessionChanged;

        var winnerShown = _activity == DashboardActivity.ShowingWinner;
        if (IsSpinInProgress)
            FinishSpin();
        if (winnerShown)
            OverlayService.BroadcastCloseWinner();

        _lifetimeCts.Cancel();
        _playedRefreshCts?.Cancel();
        _playedRefreshCts?.Dispose();
        _wheelCts.Cancel();
        _wheelCts.Dispose();
        await InvokePageCleanupAsync(SpinnerInteropMethods.DisposeDashboardBindings);

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        await InvokePageCleanupAsync("document.body.classList.remove", "spinner-page");
        await InvokePageCleanupAsync(SpinnerInteropMethods.ResetBackground);

        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    // Disposal also runs when the whole window closes, after the WebView and its scripts are gone.
    private async Task InvokePageCleanupAsync(string identifier, params object?[] args)
    {
        try
        {
            await JS.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            // The WebView has closed, which leaves no page to clean up.
        }
        catch (JSException ex)
        {
            Logger.LogWarning(ex, "Dashboard page cleanup {Function} failed", identifier);
        }
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
            catch (Exception ex)
            {
                // Windows secure storage failures have no documented exception type. Setup lets the user
                // enter the credential again, so an unreadable one is treated as missing.
                Logger.LogWarning(ex, "The API credential could not be read; opening Setup");
                credential = null;
            }

            if (credential is null)
            {
                Navigation.NavigateTo("/setup", replace: true);
                return;
            }

            _overlayHealth = OverlayServer.GetHealth();
            OverlayServer.HealthChanged += OnOverlayHealthChanged;
            _overlayHealthSubscribed = true;
            await JS.InvokeVoidAsync("document.body.classList.add", "spinner-page");
            var settings = LocalSettings.LoadSettings();
            _config = SettingsDtoConverter.ToSpinnerConfig(settings);
            _preferMarkWinnerPlayed = settings.UpdateQueueAfterSpin && !settings.DisplayNowPlaying;
            _isLockedDefault = _config.Streamer.HideChangeOptionWhenDefault
                               && !string.IsNullOrWhiteSpace(_config.Streamer.DefaultName);
            StreamerSession.UpdateConfig(_config);
            ApplySessionSnapshot(StreamerSession.GetSnapshot());
            StreamerSession.Changed += OnStreamerSessionChanged;

            _loading = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_jsInitialized) return;
        _jsInitialized = true;
        OverlayService.BroadcastWheelVisibility(_wheelVisible);

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
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetupResizeHandlers, _dotNetRef, nameof(OnResizeEnd));

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
        if (IsBusy) return;
        var name = _streamerInput.Trim();
        _streamerInputError = null;
        if (string.IsNullOrEmpty(name))
        {
            _streamerInputError = "Enter a streamer name before loading.";
            SetStatus("Please enter a streamer name");
            return;
        }

        var previousSession = StreamerSession.GetSnapshot();
        _activity = DashboardActivity.LoadingChannel;
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
            Logger.LogInformation(
                "Loaded channel {Streamer} (streamer {StreamerId}) with {AvailableSongCount} spinnable songs",
                name,
                streamerId,
                _availableSongs.Count);
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
            Logger.LogError(ex, "Loading channel {Streamer} failed", name);
        }
        finally
        {
            _activity = DashboardActivity.Idle;
        }

        StateHasChanged();
    }

    private async Task Spin()
    {
        if (IsSpinInProgress)
        {
            SetStatus("Choose what happens to the current winner before spinning again.");
            return;
        }

        if (IsBusy)
        {
            SetStatus("Wait for the current queue update to finish.");
            return;
        }

        if (string.IsNullOrEmpty(_currentStreamer))
        {
            SetStatus("Please enter a streamer name first");
            return;
        }

        if (Spins.IsCoolingDown)
        {
            SetStatus("Cooldown active");
            return;
        }

        var spinStreamer = _currentStreamer;
        _activity = DashboardActivity.Spinning;
        _winnerQueueId = null;
        _winnerQueuePosition = null;
        _wheelCts.Cancel();
        _wheelCts = new CancellationTokenSource();
        SetStatus("Fetching queue...");
        StateHasChanged();

        try
        {
            var draw = await TrackApiHealthAsync(
                Spins.DrawAsync(spinStreamer, _config, _lifetimeCts.Token),
                _lifetimeCts.Token);
            _nowPlaying = draw.NowPlaying;
            _playedSongs = draw.PlayedSongs;
            _availableSongs = draw.AvailableSongs;
            await RebuildWheel(_wheelCts.Token);

            if (draw.WinnerIndex is not { } winnerIndex)
            {
                Spins.Start(draw);
                SetStatus("No songs left to spin!");
                FinishSpin();
                await InvokeAsync(StateHasChanged);
                return;
            }

            SetStatus("Spinning...");
            await InvokeAsync(StateHasChanged);
            Spins.Start(draw);
            await JS.InvokeVoidAsync(
                SpinnerInteropMethods.SpinToItem,
                winnerIndex,
                (int)WheelSpinService.SpinDuration.TotalMilliseconds);

            var winner = await Spins.RevealWinnerAsync(draw, _lifetimeCts.Token);
            _winnerQueueId = winner.Song.QueueId;
            await ShowWinnerModalAsync(winner.Fields, winner.QueuePosition);
            OverlayService.BroadcastWinnerReveal(winner.Fields, winner.QueuePosition);
            SetStatus($"Winner: {SpinnerDataService.BuildWheelLabel(winner.Song)}");
            StateHasChanged();
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Spin for {Streamer} failed", spinStreamer);
            SetStatus($"Error: {ex.Message}");
            FinishSpin();
            StateHasChanged();
        }
    }

    private async Task ChangeStreamer()
    {
        if (IsBusy) return;
        await StreamerSession.ClearAsync(_config);
        _showStreamerInput = true;
        _currentStreamer = "";
        StreamerInput = "";
        _streamerId = 0;
        _nowPlaying = null;
        _availableSongs = [];
        _playedSongs = [];
        _winnerQueueId = null;
        _winnerQueuePosition = null;
        await RebuildWheel(_wheelCts.Token);
        StateHasChanged();
    }

    private async Task OnWheelToggle(ChangeEventArgs e)
    {
        _wheelVisible = (bool)(e.Value ?? true);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetWheelVisible, _wheelVisible);
        OverlayService.BroadcastWheelVisibility(_wheelVisible);
    }

    private async Task ToggleCollapse()
    {
        _playedListCollapsed = !_playedListCollapsed;
        await JS.InvokeVoidAsync(SpinnerInteropMethods.SetPlayedListCollapsed,
            _playedListCollapsed, _config.SongList.PlayedListPosition);
        OverlayService.UpdatePlayedListCollapsed(_playedListCollapsed);
    }

    private async Task MarkNowPlayingPlayedAsync()
    {
        if (IsBusy || _nowPlaying is null) return;
        if (_streamerId <= 0 || string.IsNullOrWhiteSpace(_currentStreamer))
        {
            SetStatus("The current streamer is unavailable. Reload the streamer and try again.");
            return;
        }

        var streamerId = _streamerId;
        var streamer = _currentStreamer;
        var markedPlayed = false;
        _activity = DashboardActivity.MarkingNowPlaying;
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
                Logger.LogError(ex, "Refreshing {Streamer} after marking Now Playing as played failed", streamer);
            }
            else
            {
                Logger.LogError(ex, "Marking Now Playing as played failed for streamer {StreamerId}", streamerId);
                SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
                SetStatus($"StreamerSongList failed while marking Now Playing as played: {ex.Message}");
            }
        }
        finally
        {
            _activity = DashboardActivity.Idle;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ShowWinnerModalAsync(WinnerDialogField[] fields, int? queuePosition)
    {
        _winnerFields = fields;
        _winnerQueuePosition = queuePosition;
        _winnerActionError = null;
        _winnerActionPending = false;
        _activity = DashboardActivity.ShowingWinner;
        await InvokeAsync(StateHasChanged);
        try
        {
            await JS.InvokeVoidAsync(
                SpinnerInteropMethods.OpenWinnerDialog,
                PreferredWinnerActionId,
                _dotNetRef,
                nameof(OnWinnerDialogCancelled));
        }
        catch
        {
            _activity = DashboardActivity.Spinning;
            await InvokeAsync(StateHasChanged);
            throw;
        }

        // Confetti is decoration: the winner reveal does not wait for it or fail with it.
        JS.InvokeVoidAsync(SpinnerInteropMethods.RunConfetti, (object)_config.WheelColors)
            .AsTask()
            .ObserveFaults(ex => Logger.LogWarning(ex, "Winner confetti failed"));
    }

    [JSInvokable]
    public Task OnWinnerDialogCancelled() => LeaveWinnerInQueueAsync();

    private Task MarkWinnerPlayedAsync()
    {
        return ExecuteWinnerActionAsync(
            "marking the winner played",
            "Winner marked as played.",
            WinnerActions.MarkPlayedAsync);
    }

    private Task SetWinnerNowPlayingAsync()
    {
        return ExecuteWinnerActionAsync(
            "updating Now Playing",
            "Winner promoted to Now Playing.",
            WinnerActions.PromoteToNowPlayingAsync);
    }

    private async Task LeaveWinnerInQueueAsync()
    {
        if (_winnerActionPending || _activity != DashboardActivity.ShowingWinner) return;

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

    // WinnerActionService logs the outcome; this shows it.
    private async Task ExecuteWinnerActionAsync(
        string actionDescription,
        string successMessage,
        Func<int, CancellationToken, Task> action)
    {
        if (_winnerActionPending || _activity != DashboardActivity.ShowingWinner) return;
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
        FinishSpin();
        await InvokeAsync(StateHasChanged);
        await JS.InvokeVoidAsync(SpinnerInteropMethods.CloseWinnerDialog);
        OverlayService.BroadcastCloseWinner();
        SetStatus(statusMessage);

        _winnerQueueId = null;
        _winnerQueuePosition = null;
        _playedRefreshCts?.Cancel();
        _playedRefreshCts = new CancellationTokenSource();
        await RefreshAfterWinnerAsync(_playedRefreshCts.Token);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnResizeEnd(string width, string minWidth) =>
        OverlayService.UpdatePlayedListWidth(width, minWidth);

    private Task<(SpinnerQueueSnapshot queue, PlayHistoryItem[] played)> FetchQueueAndHistory(
        string streamer,
        CancellationToken cancellationToken)
    {
        return TrackApiHealthAsync(FetchAsync(), cancellationToken);

        async Task<(SpinnerQueueSnapshot queue, PlayHistoryItem[] played)> FetchAsync()
        {
            var period = _config.SongList.PlayHistoryPeriod;
            var channel = new StreamerSongListChannel(streamer, _config.Streamer.Platform);
            var queueTask = ApiService.FetchQueueSnapshotAsync(channel, cancellationToken);
            var historyTask = ApiService.FetchPlayHistoryAsync(channel, period, cancellationToken);
            await Task.WhenAll(queueTask, historyTask);
            return (await queueTask, await historyTask);
        }
    }

    // API health follows the Dashboard's queue fetches. A spin's later steps fail for other reasons, such as
    // the wheel script, so they leave it alone.
    private async Task<T> TrackApiHealthAsync<T>(Task<T> queueFetch, CancellationToken cancellationToken)
    {
        try
        {
            var result = await queueFetch;
            SetApiHealth(
                StreamerSessionHealth.Healthy,
                $"Queue and history last synchronized at {TimeProvider.GetLocalNow():t}.");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            SetApiHealth(StreamerSessionHealth.Failed, ex.Message);
            throw;
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
    }

    private async Task OnStreamerKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await LoadStreamer();
    }

    private async Task RefreshAfterWinnerAsync(CancellationToken ct)
    {
        try
        {
            if (IsSpinInProgress || string.IsNullOrEmpty(_currentStreamer)) return;
            await RefreshSnapshotAsync(_currentStreamer, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus($"Post-spin refresh failed: {ex.Message}");
            Logger.LogError(ex, "Refreshing {Streamer} after the winner action failed", _currentStreamer);
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
        if (_activity == DashboardActivity.LoadingChannel || _lifetimeCts.IsCancellationRequested) return;

        try
        {
            InvokeAsync(async () =>
            {
                if (_lifetimeCts.IsCancellationRequested || _activity == DashboardActivity.LoadingChannel) return;

                ApplySessionSnapshot(e.Snapshot);
                if (_jsInitialized && !IsSpinInProgress)
                    await RebuildWheel(_wheelCts.Token);
                if (!string.IsNullOrWhiteSpace(e.Announcement))
                    SetStatus(e.Announcement);
                StateHasChanged();
            }).ObserveFaults(ex => Logger.LogError(ex, "Applying a streamer session update to the dashboard failed"));
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
        var health = OverlayServer.GetHealth();
        try
        {
            InvokeAsync(() =>
            {
                _overlayHealth = health;
                StateHasChanged();
            }).ObserveFaults(ex => Logger.LogError(ex, "Showing the overlay health on the dashboard failed"));
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

    private void FinishSpin()
    {
        _activity = DashboardActivity.Idle;
        Spins.Finish();
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
