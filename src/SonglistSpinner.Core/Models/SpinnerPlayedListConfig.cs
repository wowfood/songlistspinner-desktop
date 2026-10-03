namespace SonglistSpinner.Core.Models;

public class SpinnerPlayedListConfig
{
    public string FontFamily { get; set; } = SpinnerDefaults.FontFamily;
    public string FontSize { get; set; } = SpinnerDefaults.PlayedList.FontSize;
    public int MaxLines { get; set; } = SpinnerDefaults.PlayedList.MaxLines;
    public bool ShowNumbers { get; set; }
    public string NumberingStart { get; set; } = SpinnerSettingValues.PlayedListNumberingStarts.Default;
    public string Separator { get; set; } = SongTextFormatting.DefaultSeparator;
    public bool ShowLabels { get; set; } = SpinnerDefaults.PlayedList.ShowLabels;
    public bool ShowFieldHeaders { get; set; }
}
