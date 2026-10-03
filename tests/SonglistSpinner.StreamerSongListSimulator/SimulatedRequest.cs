namespace SonglistSpinner.Simulator;

/// <summary>One viewer's request for a queued or played song, with the amount they tipped, if any.</summary>
public sealed record SimulatedRequest(string Requester, decimal? Amount = null);
