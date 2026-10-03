namespace SonglistSpinner.Simulator;

/// <summary>
/// The publication types the simulator sends on the Centrifugo channels. Written out here rather than shared
/// with the client, so a client-side rename fails the integration tests.
/// </summary>
internal static class SimulatorEventTypes
{
    public const string NowPlayingUpdate = "now_playing_update";
    public const string QueueAdd = "queue_add";
    public const string QueueRemove = "queue_remove";
    public const string PlayHistoryAdd = "play_history_add";
}
