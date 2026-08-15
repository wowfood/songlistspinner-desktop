namespace SonglistSpinner.Services;

public enum LocalOverlayServerState
{
    Stopped,
    Starting,
    Running,
    Failed
}

public sealed record LocalOverlayHealth(
    LocalOverlayServerState ServerState,
    int ConnectedClients,
    string? Error);
