using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Core.Settings;

public class SpinnerStreamerConfig
{
    public string DefaultName { get; init; } = "";
    public string Platform { get; init; } = StreamerSongListPlatformNames.Default;
    public bool HideChangeOptionWhenDefault { get; init; } = SpinnerDefaults.Streamer.HideChangeOptionWhenDefault;
}
