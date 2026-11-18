namespace SonglistSpinner.Simulator;

/// <summary>
/// A queue entry, upcoming or Now Playing. <see cref="QueueId"/> is the id the API's queue actions take;
/// <see cref="SongId"/> is the same for every entry of the same artist and title on one simulator.
/// </summary>
public sealed record SimulatedQueueEntry(
    int QueueId,
    string Artist,
    string Title,
    int SongId,
    IReadOnlyList<SimulatedRequest> Requests);
