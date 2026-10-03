using System.Text.Json;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Data;

public static class SettingsDtoConverter
{
    // Normalizes the settings in place first, so the caller's SettingsDto holds canonical values afterwards.
    public static SpinnerConfig ToSpinnerConfig(SettingsDto settings)
    {
        SettingsDtoNormalizer.NormalizeInPlace(settings);

        var fields = SettingsDtoNormalizer.ParseFields(settings.SongListFields);
        var nowPlayingFields = SettingsDtoNormalizer.ParseFields(settings.NowPlayingFields);

        var legacyWinnerFields = SongFieldNames.NormalizeSelection(
            fields.Append(SongFieldNames.Requester),
            SongFieldNames.CreateWinnerDefaultSelection());
        var winnerDialogFields = string.IsNullOrWhiteSpace(settings.WinnerDialogFields)
            ? legacyWinnerFields
            : SettingsDtoNormalizer.ParseFields(settings.WinnerDialogFields, legacyWinnerFields);

        return new SpinnerConfig
        {
            Debug = settings.DebugMode,
            WheelColors = ParseWheelColors(settings.WheelColors),
            Background = new SpinnerBackground
            {
                Mode = settings.BackgroundMode,
                Color = settings.BackgroundColor,
                Image = settings.BackgroundImage
            },
            Streamer = new SpinnerStreamerConfig
            {
                DefaultName = settings.DefaultStreamerName,
                Platform = settings.StreamerPlatform,
                HideChangeOptionWhenDefault = settings.HideChangeOptionWhenDefault
            },
            SongList = new SpinnerSongListConfig
            {
                Fields = fields,
                ExcludePlayedSongs = settings.ExcludePlayedSongs,
                PlayedListPosition = settings.PlayedListPosition,
                PlayHistoryPeriod = settings.PlayHistoryPeriod
            },
            PlayedList = new SpinnerPlayedListConfig
            {
                FontFamily = settings.PlayedListFontFamily,
                FontSize = settings.PlayedListFontSize,
                MaxLines = settings.PlayedListMaxLines,
                ShowNumbers = settings.PlayedListShowNumbers,
                NumberingStart = SpinnerSettingValues.PlayedListNumberingStarts.NormalizeOrDefault(
                    settings.PlayedListNumberingStart),
                Separator = settings.PlayedListSeparator,
                ShowLabels = settings.PlayedListShowLabels,
                ShowFieldHeaders = settings.PlayedListShowFieldHeaders
            },
            NowPlaying = new SpinnerNowPlayingConfig
            {
                Enabled = settings.DisplayNowPlaying,
                Fields = nowPlayingFields,
                Separator = settings.NowPlayingSeparator,
                ShowLabels = settings.NowPlayingShowLabels,
                // The overlay used to substitute these defaults for empty values itself; the settings
                // preview can send an empty value before validation blocks saving it.
                FontFamily = ValueOrDefault(settings.NowPlayingFontFamily, SpinnerDefaults.FontFamily),
                FontSize = ValueOrDefault(settings.NowPlayingFontSize, SpinnerDefaults.NowPlaying.FontSize),
                Width = ValueOrDefault(settings.NowPlayingWidth, SpinnerDefaults.NowPlaying.Width),
                Position = settings.NowPlayingPosition
            },
            WinnerDialog = new SpinnerWinnerDialogConfig
            {
                Fields = winnerDialogFields,
                FontFamily = settings.WinnerDialogFontFamily,
                FontSize = settings.WinnerDialogFontSize,
                Width = settings.WinnerDialogWidth,
                ShowQueuePosition = settings.WinnerDialogShowQueuePosition
            },
            Colors = new SpinnerColors
            {
                Text = settings.ColorText,
                StatusBackground = settings.ColorStatusBackground,
                PlayedListBackground = settings.ColorPlayedListBackground,
                NowPlayingBackground = PanelBackgroundColor.Resolve(
                    settings.ColorPlayedListBackground,
                    settings.NowPlayingBackgroundOpacity),
                PlayedItemBackground = settings.ColorPlayedItemBackground,
                ResizeHandleBackground = settings.ColorResizeHandleBackground,
                ResizeHandleHoverBackground = settings.ColorResizeHandleHoverBackground,
                ToggleBackground = settings.ColorToggleBackground,
                ButtonBackground = settings.ColorButtonBackground,
                ButtonText = settings.ColorButtonText,
                Pointer = settings.ColorPointer
            }
        };
    }

    private static string[] ParseWheelColors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return SpinnerDefaults.CreateWheelColors();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? SpinnerDefaults.CreateWheelColors();
        }
        catch (JsonException)
        {
            return SpinnerDefaults.CreateWheelColors();
        }
    }

    private static string ValueOrDefault(string? value, string defaultValue) =>
        string.IsNullOrEmpty(value) ? defaultValue : value;
}
