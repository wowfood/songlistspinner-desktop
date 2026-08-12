namespace SonglistSpinner.Core.Models;

public class SpinnerStreamerConfig
{
    public string DefaultName { get; set; } = "";
    public string Platform { get; set; } = "twitch";
    public bool HideChangeOptionWhenDefault { get; set; } = true;
}
