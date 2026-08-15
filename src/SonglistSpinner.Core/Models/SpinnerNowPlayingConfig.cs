namespace SonglistSpinner.Core.Models;

public sealed class SpinnerNowPlayingConfig
{
    public bool Enabled { get; init; }
    public string[] Fields { get; init; } = ["artist", "title"];
    public string FontFamily { get; init; } = "sans-serif";
    public string FontSize { get; init; } = "1.125rem";
    public string Width { get; init; } = "28rem";
    public string Position { get; init; } = "bottom-left";
}
