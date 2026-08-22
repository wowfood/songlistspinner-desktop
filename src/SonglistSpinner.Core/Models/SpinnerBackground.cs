namespace SonglistSpinner.Core.Models;

public class SpinnerBackground
{
    public string Mode { get; set; } = SpinnerSettingValues.BackgroundModes.Default;
    public string Color { get; set; } = "#111111";
    public string Image { get; set; } = "";
}
