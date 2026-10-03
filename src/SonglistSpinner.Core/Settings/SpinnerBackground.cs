namespace SonglistSpinner.Core.Settings;

public class SpinnerBackground
{
    public string Mode { get; init; } = SpinnerSettingValues.BackgroundModes.Default;
    public string Color { get; init; } = SpinnerDefaults.Background.Color;
    public string Image { get; init; } = "";
}
