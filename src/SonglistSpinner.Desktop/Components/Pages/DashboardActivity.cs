namespace SonglistSpinner.Components.Pages;

/// <summary>
/// What the Dashboard is doing with the loaded queue. Loading a channel, marking Now Playing and a spin each
/// replace the queue, so the Dashboard runs one at a time and is busy in every state but
/// <see cref="Idle"/>. A spin lasts from the draw until the streamer chooses what happens to the winner.
/// </summary>
internal enum DashboardActivity
{
    Idle,
    LoadingChannel,
    MarkingNowPlaying,
    Spinning,
    ShowingWinner
}
