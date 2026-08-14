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
            Colors = new SpinnerColors
            {
                Text = dto.ColorText,
                StatusBackground = dto.ColorStatusBackground,
                PlayedListBackground = dto.ColorPlayedListBackground,
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
