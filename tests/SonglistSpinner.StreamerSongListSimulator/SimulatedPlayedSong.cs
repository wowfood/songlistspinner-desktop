namespace SonglistSpinner.Simulator;

/// <summary>A play-history entry: a song that was played at <see cref="PlayedAt"/>.</summary>
public sealed record SimulatedPlayedSong(
    int HistoryId,
    string Artist,
    string Title,
    int SongId,
    DateTimeOffset PlayedAt,
    IReadOnlyList<SimulatedRequest> Requests,
    decimal? DonationAmount = null);
