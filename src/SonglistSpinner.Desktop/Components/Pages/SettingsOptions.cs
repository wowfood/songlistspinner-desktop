using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Components.Pages;

internal readonly record struct SettingOption(string Value, string Label);

internal static class SettingsOptions
{
    public static IReadOnlyList<SettingOption> Platforms { get; } = Array.AsReadOnly<SettingOption>(
    [
        new(StreamerSongListPlatformNames.Twitch, "Twitch"),
        new(StreamerSongListPlatformNames.YouTube, "YouTube"),
        new(StreamerSongListPlatformNames.Kick, "Kick"),
        new(StreamerSongListPlatformNames.None, "StreamerSongList")
    ]);

    public static IReadOnlyList<SettingOption> PlayHistoryPeriods { get; } = Array.AsReadOnly<SettingOption>(
    [
        new(SpinnerSettingValues.PlayHistoryPeriods.Stream, "Recent (API v2)"),
        new(SpinnerSettingValues.PlayHistoryPeriods.Day, "Last 24 hours"),
        new(SpinnerSettingValues.PlayHistoryPeriods.Week, "Last 7 days"),
        new(SpinnerSettingValues.PlayHistoryPeriods.Month, "Last month"),
        new(SpinnerSettingValues.PlayHistoryPeriods.All, "All time")
    ]);

    public static IReadOnlyList<SettingOption> PlayedListNumberingStarts { get; } =
        Array.AsReadOnly<SettingOption>(
        [
            new(SpinnerSettingValues.PlayedListNumberingStarts.Top, "Top of list"),
            new(SpinnerSettingValues.PlayedListNumberingStarts.Bottom, "Bottom of list")
        ]);

    public static IReadOnlyList<SettingOption> PlayedListPositions { get; } = Array.AsReadOnly<SettingOption>(
    [
        new(SpinnerSettingValues.PlayedListPositions.Right, "Right"),
        new(SpinnerSettingValues.PlayedListPositions.Left, "Left")
    ]);

    public static IReadOnlyList<SettingOption> NowPlayingPositions { get; } = Array.AsReadOnly<SettingOption>(
    [
        new(SpinnerSettingValues.NowPlayingPositions.TopLeft, "Top left"),
        new(SpinnerSettingValues.NowPlayingPositions.TopCenter, "Top center"),
        new(SpinnerSettingValues.NowPlayingPositions.TopRight, "Top right"),
        new(SpinnerSettingValues.NowPlayingPositions.BottomLeft, "Bottom left"),
        new(SpinnerSettingValues.NowPlayingPositions.BottomCenter, "Bottom center"),
        new(SpinnerSettingValues.NowPlayingPositions.BottomRight, "Bottom right")
    ]);

    public static IReadOnlyList<SettingOption> BackgroundModes { get; } = Array.AsReadOnly<SettingOption>(
    [
        new(SpinnerSettingValues.BackgroundModes.Color, "Solid color"),
        new(SpinnerSettingValues.BackgroundModes.Transparent, "Transparent")
    ]);

    public static string GetSongFieldLabel(string field)
    {
        return field switch
        {
            SongFieldNames.Artist => "Artist",
            SongFieldNames.Title => "Song title",
            SongFieldNames.Requester => "Requester",
            SongFieldNames.Donation => "Donation",
            _ => field
        };
    }

    public static string GetPlatformLabel(string platform)
    {
        return Platforms.FirstOrDefault(option =>
                string.Equals(option.Value, platform, StringComparison.OrdinalIgnoreCase))
            is { Label.Length: > 0 } match
            ? match.Label
            : platform;
    }
}
