namespace SonglistSpinner.Simulator;

/// <summary>
/// One streamer's channel: its queue, Now Playing entry and play history. The public methods are the scenario
/// API: each change is published to the channel's event subscribers before the returned task completes, the
/// same as a change made through the REST API.
/// </summary>
public sealed class SimulatedChannel
{
    private readonly SimulatorContext _context;
    private readonly List<SimulatedQueueEntry> _queue = [];
    private readonly List<SimulatedPlayedSong> _history = [];
    private SimulatedQueueEntry? _nowPlaying;

    internal SimulatedChannel(int streamerId, string name, string platform, SimulatorContext context)
    {
        StreamerId = streamerId;
        Name = name;
        Platform = platform;
        _context = context;
    }

    public int StreamerId { get; }

    public string Name { get; }

    public string Platform { get; }

    /// <summary>The Centrifugo channel that queue and Now Playing changes are published on.</summary>
    public string QueueEventChannel => $"streamer:{StreamerId}-queue";

    /// <summary>The Centrifugo channel that play-history changes are published on.</summary>
    public string PlayHistoryEventChannel => $"streamer:{StreamerId}-play_history";

    /// <summary>The upcoming entries, in queue order.</summary>
    public IReadOnlyList<SimulatedQueueEntry> Queue
    {
        get
        {
            lock (_context.Gate)
                return _queue.ToArray();
        }
    }

    public SimulatedQueueEntry? NowPlaying
    {
        get
        {
            lock (_context.Gate)
                return _nowPlaying;
        }
    }

    /// <summary>Every played song, newest first.</summary>
    public IReadOnlyList<SimulatedPlayedSong> PlayHistory
    {
        get
        {
            lock (_context.Gate)
                return _history.OrderByDescending(song => song.PlayedAt).ToArray();
        }
    }

    /// <summary>A viewer requests a song: it joins the end of the queue and <c>queue_add</c> is published.</summary>
    public async Task<SimulatedQueueEntry> RequestSongAsync(
        string artist,
        string title,
        string requester,
        decimal? amount = null)
    {
        SimulatedQueueEntry entry;
        lock (_context.Gate)
        {
            entry = new SimulatedQueueEntry(
                _context.NextQueueId(),
                artist,
                title,
                _context.SongIdFor(artist, title),
                [new SimulatedRequest(requester, amount)]);
            _queue.Add(entry);
        }

        await _context.Events.PublishAsync(QueueEventChannel, SimulatorEventTypes.QueueAdd, entry.QueueId);
        return entry;
    }

    /// <summary>Adds a song that was played at <paramref name="playedAt"/> and publishes <c>play_history_add</c>.</summary>
    public async Task<SimulatedPlayedSong> AddPlayedSongAsync(
        string artist,
        string title,
        DateTimeOffset playedAt,
        string? requester = null,
        decimal? donationAmount = null)
    {
        SimulatedPlayedSong played;
        lock (_context.Gate)
        {
            played = new SimulatedPlayedSong(
                _context.NextHistoryId(),
                artist,
                title,
                _context.SongIdFor(artist, title),
                playedAt,
                requester is null ? [] : [new SimulatedRequest(requester)],
                donationAmount);
            _history.Add(played);
        }

        await _context.Events.PublishAsync(PlayHistoryEventChannel, SimulatorEventTypes.PlayHistoryAdd, played.HistoryId);
        return played;
    }

    /// <summary>
    /// The streamer marks an entry played somewhere other than this app: the upcoming or Now Playing entry
    /// moves into play history, stamped with the simulator's clock.
    /// </summary>
    /// <exception cref="InvalidOperationException">The channel has no entry with that id.</exception>
    public async Task MarkPlayedAsync(int queueId)
    {
        if (!await TryMarkPlayedAsync(queueId))
            throw new InvalidOperationException($"Queue entry {queueId} is not in {Name}'s queue.");
    }

    /// <summary>The streamer makes an upcoming entry Now Playing somewhere other than this app.</summary>
    /// <exception cref="InvalidOperationException">The queue has no upcoming entry with that id.</exception>
    public async Task SetNowPlayingAsync(int queueId)
    {
        if (!await TryPromoteAsync(queueId))
            throw new InvalidOperationException($"Queue entry {queueId} is not in {Name}'s upcoming queue.");
    }

    internal bool HasEntry(int queueId)
    {
        lock (_context.Gate)
            return _nowPlaying?.QueueId == queueId || _queue.Any(entry => entry.QueueId == queueId);
    }

    internal (SimulatedQueueEntry[] Upcoming, SimulatedQueueEntry? Playing) ReadQueue()
    {
        lock (_context.Gate)
            return (_queue.ToArray(), _nowPlaying);
    }

    internal SimulatedPlayedSong[] ReadHistory()
    {
        lock (_context.Gate)
            return _history.ToArray();
    }

    /// <summary><c>POST /queue/played?queue_id=</c>: moves the upcoming or Now Playing entry into history.</summary>
    internal async Task<bool> TryMarkPlayedAsync(int queueId)
    {
        SimulatedPlayedSong played;
        bool wasPlaying;
        lock (_context.Gate)
        {
            var entry = _nowPlaying?.QueueId == queueId
                ? _nowPlaying
                : _queue.FirstOrDefault(candidate => candidate.QueueId == queueId);
            if (entry is null) return false;

            wasPlaying = ReferenceEquals(entry, _nowPlaying);
            if (wasPlaying) _nowPlaying = null;
            else _queue.Remove(entry);
            played = RecordPlay(entry);
        }

        await _context.Events.PublishAsync(
            QueueEventChannel,
            wasPlaying ? SimulatorEventTypes.NowPlayingUpdate : SimulatorEventTypes.QueueRemove,
            queueId);
        await _context.Events.PublishAsync(PlayHistoryEventChannel, SimulatorEventTypes.PlayHistoryAdd, played.HistoryId);
        return true;
    }

    /// <summary>
    /// <c>POST /queue/played?position=playing</c>: moves the Now Playing entry into history. Returns
    /// <see langword="false"/> when nothing is playing. The next upcoming entry is not promoted automatically.
    /// </summary>
    internal async Task<bool> TryMarkNowPlayingPlayedAsync()
    {
        int queueId;
        lock (_context.Gate)
        {
            if (_nowPlaying is null) return false;
            queueId = _nowPlaying.QueueId;
        }

        return await TryMarkPlayedAsync(queueId);
    }

    /// <summary>
    /// <c>POST /queue/{id}/play</c>: makes an upcoming entry Now Playing. An entry that was already playing goes
    /// back to the front of the queue rather than into history.
    /// </summary>
    internal async Task<bool> TryPromoteAsync(int queueId)
    {
        lock (_context.Gate)
        {
            var entry = _queue.FirstOrDefault(candidate => candidate.QueueId == queueId);
            if (entry is null) return false;

            _queue.Remove(entry);
            if (_nowPlaying is not null) _queue.Insert(0, _nowPlaying);
            _nowPlaying = entry;
        }

        await _context.Events.PublishAsync(QueueEventChannel, SimulatorEventTypes.NowPlayingUpdate, queueId);
        return true;
    }

    private SimulatedPlayedSong RecordPlay(SimulatedQueueEntry entry)
    {
        var played = new SimulatedPlayedSong(
            _context.NextHistoryId(),
            entry.Artist,
            entry.Title,
            entry.SongId,
            _context.Time.GetUtcNow(),
            entry.Requests);
        _history.Add(played);
        return played;
    }
}
