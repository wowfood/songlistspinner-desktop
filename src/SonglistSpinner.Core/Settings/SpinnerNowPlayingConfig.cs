using SonglistSpinner.Core.Songs;

namespace SonglistSpinner.Core.Settings;

public sealed class SpinnerNowPlayingConfig
{
    public bool Enabled { get; init; }
    public string[] Fields { get; init; } = SongFieldNames.CreateDefaultSelection();
    public string Separator { get; init; } = SongTextFormatting.DefaultSeparator;
    public bool ShowLabels { get; init; } = SpinnerDefaults.NowPlaying.ShowLabels;
    public string FontFamily { get; init; } = SpinnerDefaults.FontFamily;
    public string FontSize { get; init; } = SpinnerDefaults.NowPlaying.FontSize;
    public string Width { get; init; } = SpinnerDefaults.NowPlaying.Width;
    public string Position { get; init; } = SpinnerSettingValues.NowPlayingPositions.Default;
}
