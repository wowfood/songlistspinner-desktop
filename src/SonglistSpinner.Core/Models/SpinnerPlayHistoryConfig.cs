namespace SonglistSpinner.Core.Models;

/// <summary>Which play history the app reads, and whether songs in it are kept off the wheel.</summary>
public class SpinnerPlayHistoryConfig
{
    public bool ExcludePlayedSongs { get; init; }

    /// <summary>A <see cref="SpinnerSettingValues.PlayHistoryPeriods"/> value.</summary>
    public string Period { get; init; } = SpinnerSettingValues.PlayHistoryPeriods.Default;
}
