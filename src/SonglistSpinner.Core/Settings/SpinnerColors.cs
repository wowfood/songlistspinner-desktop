using System.Diagnostics.CodeAnalysis;

namespace SonglistSpinner.Core.Settings;

public class SpinnerColors
{
    public string Text { get; init; } = SpinnerDefaults.Colors.Text;
    public string StatusBackground { get; init; } = SpinnerDefaults.Colors.PanelBackground;
    public string PlayedListBackground { get; init; } = SpinnerDefaults.Colors.PanelBackground;
    public string NowPlayingBackground { get; init; } = SpinnerDefaults.Colors.PanelBackground;
    public string PlayedItemBackground { get; init; } = SpinnerDefaults.Colors.PlayedItemBackground;
    public string ResizeHandleBackground { get; init; } = SpinnerDefaults.Colors.ResizeHandleBackground;
    public string ResizeHandleHoverBackground { get; init; } = SpinnerDefaults.Colors.ResizeHandleHoverBackground;
    public string ToggleBackground { get; init; } = SpinnerDefaults.Colors.ToggleBackground;
    public string ButtonBackground { get; init; } = SpinnerDefaults.Colors.ButtonBackground;
    public string ButtonText { get; init; } = SpinnerDefaults.Colors.ButtonText;

    [SuppressMessage("Naming", "CA1720:Identifier contains type name",
        Justification = "The wheel's pointer is the domain name for this colour.")]
    public string Pointer { get; init; } = SpinnerDefaults.Colors.Pointer;
}
