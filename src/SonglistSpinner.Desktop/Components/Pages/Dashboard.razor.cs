using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Components.Pages;

// Injected properties are generated from @inject directives in Dashboard.razor.
public partial class Dashboard
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private List<SpinnerQueueItem> _availableSongs = [];

    private SpinnerConfig _config = new();
    private string _currentStreamer = "";

    private DotNetObjectReference<Dashboard>? _dotNetRef;
    private Task? _eventRefreshTask;
    private Channel<bool>? _eventRefreshSignals;
    private CancellationTokenSource? _eventSubscriptionCts;
    private Task? _eventSubscriptionTask;
    private bool _isLockedDefault;
    private bool _isSpinning;
    private bool _jsInitialized;
    private DateTime _lastSpinTime = DateTime.MinValue;
    private bool _loading = true;
    private SpinnerQueueItem? _nowPlaying;
    private bool _playedListCollapsed;
    private CancellationTokenSource? _playedRefreshCts;
    private PlayHistoryItem[] _playedSongs = [];
    private bool _showStreamerInput = true;
    private string _spinButtonText = "SPIN";

    private bool _spinDisabled;

    private string _status = "";
    private bool _statusVisible;

    private string _streamerInput = "";
    private int _streamerId;
    private CancellationTokenSource _wheelCts = new();

    private bool _wheelVisible = true;
    private string _winnerDetails = "";
    private string _winnerMainLine = "";
    private int? _winnerQueueId;
    private bool _winnerVisible;

    public async ValueTask DisposeAsync()
    {
        _lifetimeCts.Cancel();
        await StopRealtimeUpdatesAsync();
        _playedRefreshCts?.Cancel();
        _playedRefreshCts?.Dispose();
        _wheelCts.Cancel();
        _wheelCts.Dispose();
        try
        {
            await JS.InvokeVoidAsync("SpinnerInterop.disposeDashboardBindings");
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
            await JS.InvokeVoidAsync("SpinnerInterop.resetBackground");
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
            await JS.InvokeVoidAsync("document.body.classList.add", "spinner-page");
            _config = LocalSettings.ToSpinnerConfig(LocalSettings.LoadSettings());
            _isLockedDefault = _config.Streamer.HideChangeOptionWhenDefault
                               && !string.IsNullOrWhiteSpace(_config.Streamer.DefaultName);

            _loading = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_jsInitialized) return;
        _jsInitialized = true;

        await JS.InvokeVoidAsync("SpinnerInterop.applyTheme", _config.Colors, _config.PlayedList);
        await JS.InvokeVoidAsync("SpinnerInterop.applyBackground", _config.Background);
        await JS.InvokeVoidAsync("SpinnerInterop.applyPlayedListPosition",
            _config.SongList.PlayedListPosition);

        await JS.InvokeVoidAsync("SpinnerInterop.createWheel",
            new[] { new { label = "Enter streamer name above" } },
            _config.WheelColors);

        await JS.InvokeVoidAsync("SpinnerInterop.setupResizeObserver");
        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("SpinnerInterop.setupResizeHandlers", _dotNetRef);

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
            SetStatus($"Error: {ex.Message}");
        }

        StateHasChanged();
    }

    private async Task Spin()
    {
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

        _lastSpinTime = DateTime.UtcNow;
        _spinDisabled = true;
        _isSpinning = true;
        _winnerQueueId = null;
        _wheelCts.Cancel();
        _wheelCts.Dispose();
        _wheelCts = new CancellationTokenSource();
        SetStatus("Fetching queue...");
        StateHasChanged();

        try
        {
            var (queue, played) = await FetchQueueAndHistory(_currentStreamer, _lifetimeCts.Token);
            _nowPlaying = queue.Playing;
            _playedSongs = played;
            _availableSongs = SpinnerDataService.FilterAvailableSongs(queue.Items, played, _config);

            if (_availableSongs.Count == 0)
            {
                SetStatus("No songs left to spin!");
                _spinDisabled = false;
                _isSpinning = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await RebuildWheel(_wheelCts.Token);
            var winnerIndex = Random.Shared.Next(_availableSongs.Count);
            SetStatus("Spinning...");
            await InvokeAsync(StateHasChanged);

            var spinWinner = _availableSongs[winnerIndex];
            var spinMainLine = $"{spinWinner.Song.Artist} - {spinWinner.Song.Title}";
            var spinDetails =
                SpinnerDataService.CreateSongTextForFields(spinWinner, SpinnerDataService.GetWinnerFields(_config));
            _ = OverlayService.BroadcastSpinCommandAsync(winnerIndex, 5000, spinMainLine, spinDetails);

            await JS.InvokeVoidAsync("SpinnerInterop.spinToItem", winnerIndex, 5000);

            var winner = _availableSongs[winnerIndex];
            await Task.Delay(5100, _lifetimeCts.Token);
            ShowWinnerModal(winner);
            _winnerQueueId = winner.QueueId;
            SetStatus($"Winner: {SpinnerDataService.BuildWheelLabel(winner)}");
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
            _isSpinning = false;
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
        StateHasChanged();
    }

    private async Task OnWheelToggle(ChangeEventArgs e)
    {
        _wheelVisible = (bool)(e.Value ?? true);
        await JS.InvokeVoidAsync("SpinnerInterop.setWheelVisible", _wheelVisible);
        _ = OverlayService.BroadcastAsync("set_wheel_visible", new { visible = _wheelVisible });
    }

    private async Task ToggleCollapse()
    {
        _playedListCollapsed = !_playedListCollapsed;
        await JS.InvokeVoidAsync("SpinnerInterop.setPlayedListCollapsed",
            _playedListCollapsed, _config.SongList.PlayedListPosition);
        await OverlayService.UpdatePlayedListCollapsedAsync(_playedListCollapsed);
    }

    private void ShowWinnerModal(SpinnerQueueItem song)
    {
        _winnerMainLine = $"{song.Song.Artist} - {song.Song.Title}";
        _winnerDetails = SpinnerDataService.CreateSongTextForFields(
            song, SpinnerDataService.GetWinnerFields(_config));
        _winnerVisible = true;
        _ = JS.InvokeVoidAsync("SpinnerInterop.runConfetti", (object)_config.WheelColors);
    }

    private async Task CloseWinnerModal()
    {
        _winnerVisible = false;
        _isSpinning = false;
        _ = OverlayService.BroadcastCloseWinnerAsync();
        var settings = LocalSettings.LoadSettings();
        if (_winnerQueueId is { } queueId && (settings.DisplayNowPlaying || settings.UpdateQueueAfterSpin))
        {
            try
            {
                if (settings.DisplayNowPlaying)
                {
                    await TransitionWinnerToNowPlayingAsync(queueId, _lifetimeCts.Token);
                    SetStatus("Winner promoted to Now Playing.");
                }
                else
                {
                    await ApiService.MarkQueueItemAsPlayedAsync(queueId, _lifetimeCts.Token);
                }
            }
            catch (Exception ex)
            {
                var action = settings.DisplayNowPlaying ? "updating Now Playing" : "marking it played";
                SetStatus($"The winner closed, but {action} failed: {ex.Message}");
            }
        }

        _winnerQueueId = null;
        _playedRefreshCts?.Cancel();
        _playedRefreshCts = new CancellationTokenSource();
        await RefreshAfterWinnerAsync(_playedRefreshCts.Token);
        StateHasChanged();
    }

    [JSInvokable]
    public Task OnResizeEnd(string width, string minWidth) =>
        OverlayService.UpdatePlayedListWidthAsync(width, minWidth);

    private async Task<(SpinnerQueueSnapshot queue, PlayHistoryItem[] played)> FetchQueueAndHistory(
        string streamer,
        CancellationToken cancellationToken)
    {
        var period = _config.SongList.PlayHistoryPeriod;
        var channel = new StreamerSongListChannel(streamer, _config.Streamer.Platform);
        var queueTask = ApiService.FetchQueueSnapshotAsync(channel, cancellationToken);
        var historyTask = ApiService.FetchPlayHistoryAsync(channel, period, cancellationToken);
        await Task.WhenAll(queueTask, historyTask);
        return (await queueTask, await historyTask);
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
        await JS.InvokeVoidAsync("SpinnerInterop.createWheel", ct, items, _config.WheelColors);
    }

    private void SetStatus(string message, bool visible = true)
    {
        _status = message;
        _statusVisible = visible && !string.IsNullOrWhiteSpace(message);
        Debug.WriteLine($"[SonglistSpinner] {message}");
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
            Debug.WriteLine($"[SonglistSpinner] Post-spin refresh failed: {ex}");
            await InvokeAsync(StateHasChanged);
        }
    }

    private void StartRealtimeUpdates(int streamerId, string streamer)
    {
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
                SetStatus($"Realtime updates stopped: {ex.Message}");
                Debug.WriteLine($"[SonglistSpinner] Realtime updates stopped: {ex}");
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

                var isSpinning = false;
                await InvokeAsync(() => { isSpinning = _isSpinning; });
                while (isSpinning)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                    await InvokeAsync(() => { isSpinning = _isSpinning; });
                }

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
                SetStatus($"Realtime refresh failed: {ex.Message}");
                Debug.WriteLine($"[SonglistSpinner] Realtime refresh failed: {ex}");
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
}
