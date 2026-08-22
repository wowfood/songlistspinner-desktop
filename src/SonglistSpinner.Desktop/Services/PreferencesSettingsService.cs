using System.Text.Json;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Services;

public sealed class PreferencesSettingsService : ILocalSettingsService
{
    private const string SettingsKey = "local_settings";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    public SettingsDto LoadSettings()
    {
        var json = Preferences.Get(SettingsKey, null);
        if (string.IsNullOrEmpty(json)) return new SettingsDto();
        try
        {
            return JsonSerializer.Deserialize<SettingsDto>(json, JsonOpts) ?? new SettingsDto();
        }
        catch
        {
            return new SettingsDto();
        }
    }

    public void SaveSettings(SettingsDto dto)
    {
        Preferences.Set(SettingsKey, JsonSerializer.Serialize(dto, JsonOpts));
    }

    public SpinnerConfig ToSpinnerConfig(SettingsDto dto)
    {
        string[] wheelColors;
        try
        {
            wheelColors = JsonSerializer.Deserialize<string[]>(dto.WheelColors, JsonOpts) ??
                          SpinnerConfig.DefaultWheelColors;
        }
        catch
        {
            wheelColors = SpinnerConfig.DefaultWheelColors;
        }

        string[] fields;
        try
        {
            fields = JsonSerializer.Deserialize<string[]>(dto.SongListFields, JsonOpts) ?? ["artist", "title"];
        }
        catch
        {
            fields = ["artist", "title"];
        }

        string[] nowPlayingFields;
        try
        {
            nowPlayingFields = JsonSerializer.Deserialize<string[]>(dto.NowPlayingFields, JsonOpts) ??
                               ["artist", "title"];
        }
        catch
        {
            nowPlayingFields = ["artist", "title"];
        }

        var legacyWinnerFields = (fields.Length > 0 ? fields : ["artist", "title"])
            .Select(field => field.ToLowerInvariant())
            .Append("requester")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] winnerDialogFields;
        try
        {
            winnerDialogFields = string.IsNullOrWhiteSpace(dto.WinnerDialogFields)
                ? legacyWinnerFields
                : JsonSerializer.Deserialize<string[]>(dto.WinnerDialogFields, JsonOpts) ?? legacyWinnerFields;
        }
        catch
        {
            winnerDialogFields = legacyWinnerFields;
        }

        return new SpinnerConfig
        {
            Debug = dto.DebugMode,
            WheelColors = wheelColors,
            Background = new SpinnerBackground
            {
                Mode = dto.BackgroundMode,
                Color = dto.BackgroundColor,
                Image = dto.BackgroundImage
            },
            Streamer = new SpinnerStreamerConfig
            {
                DefaultName = dto.DefaultStreamerName,
                Platform = dto.StreamerPlatform,
                HideChangeOptionWhenDefault = dto.HideChangeOptionWhenDefault
            },
            SongList = new SpinnerSongListConfig
            {
                Fields = fields,
                ExcludePlayedSongs = dto.ExcludePlayedSongs,
                PlayedListPosition = dto.PlayedListPosition,
                PlayHistoryPeriod = dto.PlayHistoryPeriod
            },
            PlayedList = new SpinnerPlayedListConfig
            {
                FontFamily = dto.PlayedListFontFamily,
                FontSize = dto.PlayedListFontSize,
                MaxLines = dto.PlayedListMaxLines
            },
            NowPlaying = new SpinnerNowPlayingConfig
            {
                Enabled = dto.DisplayNowPlaying,
                Fields = nowPlayingFields,
                FontFamily = dto.NowPlayingFontFamily,
                FontSize = dto.NowPlayingFontSize,
                Width = dto.NowPlayingWidth,
                Position = dto.NowPlayingPosition
            },
            WinnerDialog = new SpinnerWinnerDialogConfig
            {
                Fields = winnerDialogFields,
                FontFamily = dto.WinnerDialogFontFamily,
                FontSize = dto.WinnerDialogFontSize,
                Width = dto.WinnerDialogWidth,
                ShowQueuePosition = dto.WinnerDialogShowQueuePosition
            },
            Colors = new SpinnerColors
            {
                Text = dto.ColorText,
                StatusBackground = dto.ColorStatusBackground,
                PlayedListBackground = dto.ColorPlayedListBackground,
                NowPlayingBackground = PanelBackgroundColor.Resolve(
                    dto.ColorPlayedListBackground,
                    dto.NowPlayingBackgroundOpacity),
                PlayedItemBackground = dto.ColorPlayedItemBackground,
                ResizeHandleBackground = dto.ColorResizeHandleBackground,
                ResizeHandleHoverBackground = dto.ColorResizeHandleHoverBackground,
                ToggleBackground = dto.ColorToggleBackground,
                ButtonBackground = dto.ColorButtonBackground,
                ButtonText = dto.ColorButtonText,
                Pointer = dto.ColorPointer
            }
        };
    }
}
