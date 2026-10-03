using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.PlayedSongs;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.Winner;

namespace SonglistSpinner.Services;

/// <summary>
/// Runs one spin of the wheel for the loaded channel, in the order the Dashboard calls it:
/// <see cref="DrawAsync"/> fetches the queue and picks the winner, <see cref="Start"/> publishes the
/// queue and starts the overlay's wheel, <see cref="RevealWinnerAsync"/> waits for the wheel to stop, and
/// <see cref="Finish"/> ends the spin. Session refreshes are suspended from the draw until
/// <see cref="Finish"/>, so a realtime update never replaces the songs on a turning wheel or under an
/// unresolved winner. The caller must call <see cref="Finish"/> however the spin ends.
/// </summary>
public sealed class WheelSpinService
{
    public static readonly TimeSpan SpinDuration = TimeSpan.FromSeconds(5);

    /// <summary>The pause after the wheel stops before the winner is revealed.</summary>
    public static readonly TimeSpan WinnerRevealDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The minimum time between the starts of two spins.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    /// <summary>How long the winner's current queue position is looked up before it is shown without it.</summary>
    public static readonly TimeSpan QueuePositionLookupTimeout = TimeSpan.FromSeconds(2);

    private readonly IStreamerSongListClient _songListClient;
    private readonly StreamerSessionService _session;
    private readonly OverlayStateService _overlay;
    private readonly Random _winnerPicker;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WheelSpinService> _logger;
    private DateTimeOffset _lastDrawAt = DateTimeOffset.MinValue;

    public WheelSpinService(
        IStreamerSongListClient songListClient,
        StreamerSessionService session,
        OverlayStateService overlay,
        Random winnerPicker,
        TimeProvider? timeProvider = null,
        ILogger<WheelSpinService>? logger = null)
    {
        _songListClient = songListClient;
        _session = session;
        _overlay = overlay;
        _winnerPicker = winnerPicker;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<WheelSpinService>.Instance;
    }

    public bool IsCoolingDown => _timeProvider.GetUtcNow() - _lastDrawAt < Cooldown;

    /// <summary>
    /// Starts a spin: suspends session refreshes, fetches the current queue and play history, and picks the
    /// winner from the songs that are still eligible.
    /// </summary>
    public async Task<SpinDraw> DrawAsync(string streamer, SpinnerConfig config, CancellationToken cancellationToken)
    {
        _lastDrawAt = _timeProvider.GetUtcNow();
        _session.SetRefreshSuspended(true);

        var channel = new StreamerSongListChannel(streamer, config.Streamer.Platform);
        var queueTask = _songListClient.FetchQueueSnapshotAsync(channel, cancellationToken);
        var historyTask = _songListClient.FetchPlayHistoryAsync(channel, config.PlayHistory.Period, cancellationToken);
        await Task.WhenAll(queueTask, historyTask);
        var queue = await queueTask;
        var played = await historyTask;

        var availableSongs = SongAvailability.FilterAvailableSongs(queue.Items, played, config);
        int? winnerIndex = availableSongs.Count == 0 ? null : _winnerPicker.Next(availableSongs.Count);
        return new SpinDraw(streamer, config, availableSongs, played, queue.Playing, winnerIndex);
    }

    /// <summary>
    /// Publishes the drawn queue to the session and the overlay, then tells the overlay to spin to the winner.
    /// A draw with no winner publishes the empty queue so nothing shows songs that have gone.
    /// </summary>
    public void Start(SpinDraw draw)
    {
        _session.UpdateSnapshot(draw.Config, draw.AvailableSongs, draw.PlayedSongs, draw.NowPlaying);

        if (draw is not { WinnerIndex: { } winnerIndex, Winner: { } winner })
        {
            _logger.LogInformation("Spin for {Streamer} found no songs left to spin", draw.Streamer);
            return;
        }

        _logger.LogInformation(
            "Spin for {Streamer} picked queue entry {QueueId} from {AvailableSongCount} songs",
            draw.Streamer,
            winner.QueueId,
            draw.AvailableSongs.Count);
        _overlay.BroadcastSpinCommand(winnerIndex, winner.QueueId, (int)SpinDuration.TotalMilliseconds);
    }

    /// <summary>
    /// Waits for the wheel to stop, then returns the winner as the dialog shows it, with its current queue
    /// position when the dialog shows positions.
    /// </summary>
    public async Task<SpinWinner> RevealWinnerAsync(SpinDraw draw, CancellationToken cancellationToken)
    {
        var winner = draw.Winner ?? throw new InvalidOperationException("The draw has no winner to reveal.");

        await Task.Delay(SpinDuration + WinnerRevealDelay, _timeProvider, cancellationToken);
        var queuePosition = draw.Config.WinnerDialog.ShowQueuePosition
            ? await LookUpQueuePositionAsync(draw, winner.QueueId, cancellationToken)
            : null;
        return new SpinWinner(winner, WinnerDialogContent.CreateFields(winner, draw.Config), queuePosition);
    }

    /// <summary>Ends the spin and resumes session refreshes.</summary>
    public void Finish() => _session.SetRefreshSuspended(false);

    // The position is a courtesy: a slow or failed lookup, or a different channel loaded meanwhile, shows the
    // winner without it rather than holding up the reveal.
    private async Task<int?> LookUpQueuePositionAsync(
        SpinDraw draw,
        int queueId,
        CancellationToken cancellationToken)
    {
        if (queueId <= 0 || !IsChannelStillLoaded(draw.Streamer)) return null;

        try
        {
            using var lookupTimeout = new CancellationTokenSource(QueuePositionLookupTimeout, _timeProvider);
            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lookupTimeout.Token);
            var channel = new StreamerSongListChannel(draw.Streamer, draw.Config.Streamer.Platform);
            var queue = await _songListClient.FetchQueueSnapshotAsync(channel, lookupCts.Token);
            if (!IsChannelStillLoaded(draw.Streamer)) return null;
            return WinnerDialogContent.FindQueuePosition(queue.Items, queueId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Looking up the position of queue entry {QueueId} timed out after {TimeoutMilliseconds} ms; " +
                "the winner is shown without it",
                queueId,
                QueuePositionLookupTimeout.TotalMilliseconds);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Looking up the position of queue entry {QueueId} failed; the winner is shown without it",
                queueId);
            return null;
        }
    }

    private bool IsChannelStillLoaded(string streamer) =>
        string.Equals(_session.GetSnapshot().Streamer, streamer, StringComparison.Ordinal);
}
