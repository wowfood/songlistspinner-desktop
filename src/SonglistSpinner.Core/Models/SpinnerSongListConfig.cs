namespace SonglistSpinner.Core.Models;

public class SpinnerSongListConfig
{
    public string[] Fields { get; init; } = SongFieldNames.CreateDefaultSelection();
    public bool ExcludePlayedSongs { get; init; }
    public string PlayedListPosition { get; init; } = SpinnerSettingValues.PlayedListPositions.Default;
    public string PlayHistoryPeriod { get; init; } = SpinnerSettingValues.PlayHistoryPeriods.Default;
}
