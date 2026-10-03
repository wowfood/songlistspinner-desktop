using SonglistSpinner.Core.Songs;

namespace SonglistSpinner.Core.Settings;

/// <summary>How the played-song list (in the app and the overlay) shows the songs played so far.</summary>
public class SpinnerPlayedListConfig
{
    /// <summary>The <see cref="SongFieldNames"/> fields each played song shows, in order.</summary>
    public string[] Fields { get; init; } = SongFieldNames.CreateDefaultSelection();

    /// <summary>A <see cref="SpinnerSettingValues.PlayedListPositions"/> value.</summary>
    public string Position { get; init; } = SpinnerSettingValues.PlayedListPositions.Default;

    public string FontFamily { get; init; } = SpinnerDefaults.FontFamily;
    public string FontSize { get; init; } = SpinnerDefaults.PlayedList.FontSize;
    public int MaxLines { get; init; } = SpinnerDefaults.PlayedList.MaxLines;
    public bool ShowNumbers { get; init; }
    public string NumberingStart { get; init; } = SpinnerSettingValues.PlayedListNumberingStarts.Default;
    public string Separator { get; init; } = SongTextFormatting.DefaultSeparator;
    public bool ShowLabels { get; init; } = SpinnerDefaults.PlayedList.ShowLabels;
    public bool ShowFieldHeaders { get; init; }
}
