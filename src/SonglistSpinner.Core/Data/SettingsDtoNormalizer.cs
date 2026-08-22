using System.Text.Json;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Data;

public static class SettingsDtoNormalizer
{
    public static SettingsDto Normalize(SettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.BackgroundMode = SpinnerSettingValues.BackgroundModes.NormalizeOrDefault(settings.BackgroundMode);
        settings.StreamerPlatform = StreamerSongListPlatformNames.NormalizeOrDefault(settings.StreamerPlatform);
        settings.PlayedListPosition =
            SpinnerSettingValues.PlayedListPositions.NormalizeOrDefault(settings.PlayedListPosition);
        settings.PlayHistoryPeriod =
            SpinnerSettingValues.PlayHistoryPeriods.NormalizeOrDefault(settings.PlayHistoryPeriod);
        settings.NowPlayingPosition =
            SpinnerSettingValues.NowPlayingPositions.NormalizeOrDefault(settings.NowPlayingPosition);
        settings.PlayedListNumberingStart =
            SpinnerSettingValues.PlayedListNumberingStarts.NormalizeOrDefault(settings.PlayedListNumberingStart);

        var songListFields = ParseFields(settings.SongListFields);
        settings.SongListFields = JsonSerializer.Serialize(songListFields);
        settings.NowPlayingFields = JsonSerializer.Serialize(ParseFields(settings.NowPlayingFields));

        if (!string.IsNullOrWhiteSpace(settings.WinnerDialogFields))
        {
            var legacyWinnerFields = SongFieldNames.NormalizeSelection(
                songListFields.Append(SongFieldNames.Requester),
                SongFieldNames.CreateWinnerDefaultSelection());
            settings.WinnerDialogFields = JsonSerializer.Serialize(
                ParseFields(settings.WinnerDialogFields, legacyWinnerFields));
        }

        return settings;
    }

    public static string[] ParseFields(string? json, IEnumerable<string>? fallback = null)
    {
        string[]? fields = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                fields = JsonSerializer.Deserialize<string[]>(json);
            }
            catch (JsonException)
            {
            }
        }

        return SongFieldNames.NormalizeSelection(fields, fallback);
    }
}
