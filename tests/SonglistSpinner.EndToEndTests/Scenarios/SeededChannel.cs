using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests.Scenarios;

/// <summary>What <see cref="ChannelSeed"/> put in the simulator, as it was when seeded.</summary>
/// <param name="Channel">The live simulator channel, for later changes and for its current state.</param>
/// <param name="NowPlaying">The seeded Now Playing entry, if any.</param>
/// <param name="Queue">The seeded upcoming entries, in queue order.</param>
/// <param name="Played">The seeded play history, in the order it was added (not play order).</param>
internal sealed record SeededChannel(
    SimulatedChannel Channel,
    SimulatedQueueEntry? NowPlaying,
    IReadOnlyList<SimulatedQueueEntry> Queue,
    IReadOnlyList<SimulatedPlayedSong> Played)
{
    public string Name => Channel.Name;

    public int StreamerId => Channel.StreamerId;

    /// <summary>The seeded upcoming entry for <paramref name="song"/>.</summary>
    public SimulatedQueueEntry QueueEntryFor(SongSeed song) =>
        Queue.Single(entry => entry.Artist == song.Artist && entry.Title == song.Title);

    /// <summary>
    /// The seeded upcoming entry whose artist and title are both among <paramref name="shownValues"/>, such as the
    /// values a winner dialog shows. Seeded queues never repeat a song, so at most one matches.
    /// </summary>
    /// <exception cref="InvalidOperationException">No seeded upcoming entry matches.</exception>
    public SimulatedQueueEntry QueueEntryShowing(IReadOnlyCollection<string> shownValues) =>
        Queue.SingleOrDefault(entry => shownValues.Contains(entry.Artist) && shownValues.Contains(entry.Title))
        ?? throw new InvalidOperationException(
            $"No seeded queue entry has both its artist and title among [{string.Join(", ", shownValues)}].");
}
