namespace SonglistSpinner.Core.Models;

public sealed class SpinnerWinnerDialogConfig
{
    public string[] Fields { get; init; } = SongFieldNames.CreateWinnerDefaultSelection();
    public string FontFamily { get; init; } = "sans-serif";
    public string FontSize { get; init; } = "1rem";
    public string Width { get; init; } = "36rem";
    public bool ShowQueuePosition { get; init; } = true;
}
