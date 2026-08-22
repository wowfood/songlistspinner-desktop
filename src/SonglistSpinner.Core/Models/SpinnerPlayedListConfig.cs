namespace SonglistSpinner.Core.Models;

public class SpinnerPlayedListConfig
{
    public string FontFamily { get; set; } = "sans-serif";
    public string FontSize { get; set; } = "0.875rem";
    public int MaxLines { get; set; } = 2;
    public bool ShowNumbers { get; set; }
    public string NumberingStart { get; set; } = SpinnerSettingValues.PlayedListNumberingStarts.Default;
}
