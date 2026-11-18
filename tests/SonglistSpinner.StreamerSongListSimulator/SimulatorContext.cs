namespace SonglistSpinner.Simulator;

/// <summary>
/// What every channel of one simulator shares: the lock over all queue and history state, the clock that stamps
/// plays, the event hub that publishes changes and the id counters.
/// </summary>
internal sealed class SimulatorContext(TimeProvider time, EventHub events)
{
    private readonly Dictionary<(string Artist, string Title), int> _songIds = [];
    private int _lastQueueId = 100;
    private int _lastHistoryId = 5000;

    /// <summary>Guards every channel's queue, Now Playing entry and history, and the song catalogue.</summary>
    public object Gate { get; } = new();

    public TimeProvider Time { get; } = time;

    public EventHub Events { get; } = events;

    public int NextQueueId() => Interlocked.Increment(ref _lastQueueId);

    public int NextHistoryId() => Interlocked.Increment(ref _lastHistoryId);

    /// <summary>The catalogue id for a song; the same artist and title, ignoring case, always get the same id.</summary>
    /// <remarks>Call while holding <see cref="Gate"/>.</remarks>
    public int SongIdFor(string artist, string title)
    {
        var key = (artist.ToUpperInvariant(), title.ToUpperInvariant());
        if (!_songIds.TryGetValue(key, out var id))
        {
            id = _songIds.Count + 1;
            _songIds[key] = id;
        }

        return id;
    }
}
