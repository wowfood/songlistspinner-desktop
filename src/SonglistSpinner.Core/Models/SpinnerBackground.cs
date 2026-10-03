namespace SonglistSpinner.Core.Models;

public class SpinnerBackground
{
    public string Mode { get; set; } = SpinnerSettingValues.BackgroundModes.Default;
    public string Color { get; set; } = SpinnerDefaults.Background.Color;
    public string Image { get; set; } = "";
}
