using System.Diagnostics.CodeAnalysis;

namespace SonglistSpinner.Core.Models;

public class SpinnerColors
{
    public string Text { get; set; } = SpinnerDefaults.Colors.Text;
    public string StatusBackground { get; set; } = SpinnerDefaults.Colors.PanelBackground;
    public string PlayedListBackground { get; set; } = SpinnerDefaults.Colors.PanelBackground;
    public string NowPlayingBackground { get; set; } = SpinnerDefaults.Colors.PanelBackground;
    public string PlayedItemBackground { get; set; } = SpinnerDefaults.Colors.PlayedItemBackground;
    public string ResizeHandleBackground { get; set; } = SpinnerDefaults.Colors.ResizeHandleBackground;
    public string ResizeHandleHoverBackground { get; set; } = SpinnerDefaults.Colors.ResizeHandleHoverBackground;
    public string ToggleBackground { get; set; } = SpinnerDefaults.Colors.ToggleBackground;
    public string ButtonBackground { get; set; } = SpinnerDefaults.Colors.ButtonBackground;
    public string ButtonText { get; set; } = SpinnerDefaults.Colors.ButtonText;

    [SuppressMessage("Naming", "CA1720:Identifier contains type name",
        Justification = "The wheel's pointer is the domain name for this colour.")]
    public string Pointer { get; set; } = SpinnerDefaults.Colors.Pointer;
}
