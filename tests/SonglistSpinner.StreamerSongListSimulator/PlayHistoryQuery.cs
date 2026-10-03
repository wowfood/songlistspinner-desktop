namespace SonglistSpinner.Simulator;

/// <summary>
/// The <c>GET /play_history</c> page: songs played strictly after <see cref="PlayedAfter"/> (all of them when it
/// is null), ordered by play time, at most <see cref="Limit"/> of them.
/// </summary>
/// <remarks>
/// <see cref="Total"/> counts every match before the limit is applied. Ties in play time keep the order the
/// songs were added. Only <c>order_by=played_at</c> is supported, the one ordering the client sends.
/// </remarks>
internal sealed record PlayHistoryQuery(DateTimeOffset? PlayedAfter, bool NewestFirst, int Limit)
{
    public const int MaximumLimit = 100;

    public (IReadOnlyList<SimulatedPlayedSong> Page, int Total) Apply(IEnumerable<SimulatedPlayedSong> history)
    {
        var matches = history.Where(song => PlayedAfter is null || song.PlayedAt > PlayedAfter.Value);
        var ordered = NewestFirst
            ? matches.OrderByDescending(song => song.PlayedAt)
            : matches.OrderBy(song => song.PlayedAt);
        var all = ordered.ToArray();
        return (all.Take(Limit).ToArray(), all.Length);
    }
}
