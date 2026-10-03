namespace SonglistSpinner.Core.Models;

public class SpinnerPlayedListConfig
{
    public string FontFamily { get; init; } = SpinnerDefaults.FontFamily;
    public string FontSize { get; init; } = SpinnerDefaults.PlayedList.FontSize;
    public int MaxLines { get; init; } = SpinnerDefaults.PlayedList.MaxLines;
    public bool ShowNumbers { get; init; }
    public string NumberingStart { get; init; } = SpinnerSettingValues.PlayedListNumberingStarts.Default;
    public string Separator { get; init; } = SongTextFormatting.DefaultSeparator;
    public bool ShowLabels { get; init; } = SpinnerDefaults.PlayedList.ShowLabels;
    public bool ShowFieldHeaders { get; init; }
}
