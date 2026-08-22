namespace SonglistSpinner.Core.Models;

public class SpinnerStreamerConfig
{
    public string DefaultName { get; set; } = "";
    public string Platform { get; set; } = StreamerSongListPlatformNames.Default;
    public bool HideChangeOptionWhenDefault { get; set; } = true;
}
