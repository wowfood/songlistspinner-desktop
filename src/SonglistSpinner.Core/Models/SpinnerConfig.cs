namespace SonglistSpinner.Core.Models;

public class SpinnerConfig
{
    public bool Debug { get; init; }
    public string[] WheelColors { get; init; } = SpinnerDefaults.CreateWheelColors();
    public SpinnerBackground Background { get; init; } = new();
    public SpinnerStreamerConfig Streamer { get; init; } = new();
    public SpinnerSongListConfig SongList { get; init; } = new();
    public SpinnerPlayedListConfig PlayedList { get; init; } = new();
    public SpinnerNowPlayingConfig NowPlaying { get; init; } = new();
    public SpinnerWinnerDialogConfig WinnerDialog { get; init; } = new();
    public SpinnerColors Colors { get; init; } = new();
}
