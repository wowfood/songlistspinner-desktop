using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

/// <summary>
/// Applies the streamer's choice for a revealed winner to StreamerSongList for the channel the session has
/// loaded. Each action logs its outcome; a failure is rethrown for the caller to show.
/// </summary>
public sealed class WinnerActionService : IDisposable
{
    private readonly IStreamerSongListClient _songListClient;
    private readonly NowPlayingTransitionService _nowPlayingTransitions;
    private readonly StreamerSessionService _session;
    private readonly ILogger<WinnerActionService> _logger;

    // Promotion reads the queue, may complete the current song and then promotes the winner; two
    // overlapping promotions could complete the song the other one just promoted.
    private readonly SemaphoreSlim _promotionGate = new(1, 1);

    public WinnerActionService(
        IStreamerSongListClient songListClient,
        NowPlayingTransitionService nowPlayingTransitions,
        StreamerSessionService session,
        ILogger<WinnerActionService>? logger = null)
    {
        _songListClient = songListClient;
        _nowPlayingTransitions = nowPlayingTransitions;
        _session = session;
        _logger = logger ?? NullLogger<WinnerActionService>.Instance;
    }

    public async Task MarkPlayedAsync(int queueId, CancellationToken cancellationToken)
    {
        try
        {
            await _songListClient.MarkQueueItemAsPlayedAsync(new QueueEntryId(queueId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Marking winning queue entry {QueueId} as played failed", queueId);
            throw;
        }

        _logger.LogInformation("Marked winning queue entry {QueueId} as played", queueId);
    }

    /// <summary>
    /// Makes the winner the channel's Now Playing song, completing the current one first.
    /// </summary>
    /// <exception cref="InvalidOperationException">No channel is loaded, or the winner has left the queue.</exception>
    public async Task PromoteToNowPlayingAsync(int queueId, CancellationToken cancellationToken)
    {
        try
        {
            var session = _session.GetSnapshot();
            if (session.StreamerId <= 0)
            {
                throw new InvalidOperationException(
                    "The current streamer ID is unavailable. Reload the streamer and try again.");
            }

            var channel = new StreamerSongListChannel(session.Streamer, session.Config.Streamer.Platform);
            await _promotionGate.WaitAsync(cancellationToken);
            try
            {
                await _nowPlayingTransitions.PromoteWinnerAsync(
                    channel,
                    new StreamerId(session.StreamerId),
                    new QueueEntryId(queueId),
                    cancellationToken);
            }
            finally
            {
                _promotionGate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Promoting winning queue entry {QueueId} to Now Playing failed", queueId);
            throw;
        }

        _logger.LogInformation("Promoted winning queue entry {QueueId} to Now Playing", queueId);
    }

    public void Dispose() => _promotionGate.Dispose();
}
