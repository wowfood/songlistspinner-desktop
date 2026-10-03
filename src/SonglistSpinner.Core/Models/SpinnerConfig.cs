namespace SonglistSpinner.Core.Models;

public class SpinnerConfig
{
    // Returns a new array on every call: configs hand WheelColors to callers as a mutable array.
    public static string[] CreateDefaultWheelColors() =>
    [
        "#ff6b6b", "#4ecdc4", "#45b7d1", "#f9ca24",
        "#6c5ce7", "#a29bfe", "#fd79a8", "#fdcb6e"
    ];

    public bool Debug { get; init; }
    public string[] WheelColors { get; init; } = CreateDefaultWheelColors();
    public SpinnerBackground Background { get; init; } = new();
    public SpinnerStreamerConfig Streamer { get; init; } = new();
    public SpinnerSongListConfig SongList { get; init; } = new();
    public SpinnerPlayedListConfig PlayedList { get; init; } = new();
    public SpinnerNowPlayingConfig NowPlaying { get; init; } = new();
    public SpinnerWinnerDialogConfig WinnerDialog { get; init; } = new();
    public SpinnerColors Colors { get; init; } = new();
}
