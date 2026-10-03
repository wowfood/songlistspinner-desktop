using System.Text.Json;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Data;

public static class SettingsDtoNormalizer
{
    /// <summary>
    /// Rewrites <paramref name="settings"/> so every catalog value, separator and field list is canonical,
    /// replacing values that cannot be read with their defaults.
    /// </summary>
    /// <returns>The same instance, so loading and saving can chain the call.</returns>
    /// <remarks>
    /// A saved <see cref="SettingsDto.WinnerDialogFields"/> of <c>null</c> is kept: it marks settings saved before
    /// the winner dialog had its own fields, which <see cref="SettingsDtoConverter"/> derives from the played-list
    /// fields instead.
    /// </remarks>
    public static SettingsDto NormalizeInPlace(SettingsDto settings)
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
        settings.PlayedListSeparator = SongTextFormatting.NormalizeSeparator(settings.PlayedListSeparator);
        settings.NowPlayingSeparator = SongTextFormatting.NormalizeSeparator(settings.NowPlayingSeparator);

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
