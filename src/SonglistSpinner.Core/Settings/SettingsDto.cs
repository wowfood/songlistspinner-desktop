using System.Text.Json;
using System.Text.Json.Serialization;
using SonglistSpinner.Core.Songs;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Core.Settings;

// Persisted as JSON in MAUI Preferences: property names are the wire format. Initial values come from
// SpinnerDefaults, the same source as the Spinner*Config models.
public class SettingsDto
{
    public string WheelColors { get; set; } = JsonSerializer.Serialize(SpinnerDefaults.CreateWheelColors());

    public string BackgroundMode { get; set; } = SpinnerSettingValues.BackgroundModes.Default;
    public string BackgroundColor { get; set; } = SpinnerDefaults.Background.Color;
    public string BackgroundImage { get; set; } = "";
    public string DefaultStreamerName { get; set; } = "";
    public string StreamerPlatform { get; set; } = StreamerSongListPlatformNames.Default;
    public bool HideChangeOptionWhenDefault { get; set; } = SpinnerDefaults.Streamer.HideChangeOptionWhenDefault;
    // Configures the played-song list; the wire name predates that vocabulary.
    [JsonPropertyName("SongListFields")]
    public string PlayedListFields { get; set; } = SongFieldNames.DefaultJson;
    public string PlayedListSeparator { get; set; } = SongTextFormatting.DefaultSeparator;
    public bool PlayedListShowLabels { get; set; } = SpinnerDefaults.PlayedList.ShowLabels;
    public bool PlayedListShowFieldHeaders { get; set; }
    public bool ExcludePlayedSongs { get; set; }
    public string PlayedListPosition { get; set; } = SpinnerSettingValues.PlayedListPositions.Default;
    public string PlayHistoryPeriod { get; set; } = SpinnerSettingValues.PlayHistoryPeriods.Default;
    [JsonPropertyName("AutoPlay")]
    public bool UpdateQueueAfterSpin { get; set; }
    public bool DisplayNowPlaying { get; set; }
    public string NowPlayingFields { get; set; } = SongFieldNames.DefaultJson;
    public string NowPlayingSeparator { get; set; } = SongTextFormatting.DefaultSeparator;
    public bool NowPlayingShowLabels { get; set; } = SpinnerDefaults.NowPlaying.ShowLabels;
    public string NowPlayingFontFamily { get; set; } = SpinnerDefaults.FontFamily;
    public string NowPlayingFontSize { get; set; } = SpinnerDefaults.NowPlaying.FontSize;
    public string NowPlayingWidth { get; set; } = SpinnerDefaults.NowPlaying.Width;
    public string NowPlayingPosition { get; set; } = SpinnerSettingValues.NowPlayingPositions.Default;
    public double? NowPlayingBackgroundOpacity { get; set; }
    public string? WinnerDialogFields { get; set; }
    public string WinnerDialogFontFamily { get; set; } = SpinnerDefaults.FontFamily;
    public string WinnerDialogFontSize { get; set; } = SpinnerDefaults.WinnerDialog.FontSize;
    public string WinnerDialogWidth { get; set; } = SpinnerDefaults.WinnerDialog.Width;
    public bool WinnerDialogShowQueuePosition { get; set; } = SpinnerDefaults.WinnerDialog.ShowQueuePosition;
    public bool DebugMode { get; set; }
    public string ColorText { get; set; } = SpinnerDefaults.Colors.Text;
    public string ColorStatusBackground { get; set; } = SpinnerDefaults.Colors.PanelBackground;
    public string ColorPlayedListBackground { get; set; } = SpinnerDefaults.Colors.PanelBackground;
    public string ColorPlayedItemBackground { get; set; } = SpinnerDefaults.Colors.PlayedItemBackground;
    public string ColorResizeHandleBackground { get; set; } = SpinnerDefaults.Colors.ResizeHandleBackground;
    public string ColorResizeHandleHoverBackground { get; set; } = SpinnerDefaults.Colors.ResizeHandleHoverBackground;
    public string ColorToggleBackground { get; set; } = SpinnerDefaults.Colors.ToggleBackground;
    public string ColorButtonBackground { get; set; } = SpinnerDefaults.Colors.ButtonBackground;
    public string ColorButtonText { get; set; } = SpinnerDefaults.Colors.ButtonText;
    public string ColorPointer { get; set; } = SpinnerDefaults.Colors.Pointer;
    public string PlayedListFontFamily { get; set; } = SpinnerDefaults.FontFamily;
    public string PlayedListFontSize { get; set; } = SpinnerDefaults.PlayedList.FontSize;
    public int PlayedListMaxLines { get; set; } = SpinnerDefaults.PlayedList.MaxLines;
    public bool PlayedListShowNumbers { get; set; }
    public string PlayedListNumberingStart { get; set; } = SpinnerSettingValues.PlayedListNumberingStarts.Default;
}
