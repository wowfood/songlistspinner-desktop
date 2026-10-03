using SonglistSpinner.Core.Songs;

namespace SonglistSpinner.Core.Settings;

public sealed class SpinnerWinnerDialogConfig
{
    public string[] Fields { get; init; } = SongFieldNames.CreateWinnerDefaultSelection();
    public string FontFamily { get; init; } = SpinnerDefaults.FontFamily;
    public string FontSize { get; init; } = SpinnerDefaults.WinnerDialog.FontSize;
    public string Width { get; init; } = SpinnerDefaults.WinnerDialog.Width;
    public bool ShowQueuePosition { get; init; } = SpinnerDefaults.WinnerDialog.ShowQueuePosition;
}
