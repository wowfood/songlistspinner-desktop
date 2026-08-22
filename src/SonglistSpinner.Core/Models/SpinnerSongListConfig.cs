namespace SonglistSpinner.Core.Models;

public class SpinnerSongListConfig
{
    public string[] Fields { get; set; } = SongFieldNames.CreateDefaultSelection();
    public bool ExcludePlayedSongs { get; set; }
    public string PlayedListPosition { get; set; } = SpinnerSettingValues.PlayedListPositions.Default;
    public string PlayHistoryPeriod { get; set; } = SpinnerSettingValues.PlayHistoryPeriods.Default;
}
