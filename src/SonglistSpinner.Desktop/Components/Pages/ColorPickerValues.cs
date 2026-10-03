using MudBlazor.Utilities;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// Converts the CSS colours stored in settings to and from the values the Settings colour pickers edit. A colour
/// that cannot be read shows as black rather than failing the page.
/// </summary>
public static class ColorPickerValues
{
    private const string FallbackHex = "#000000";

    public static MudColor ToMudColor(this string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return new MudColor(FallbackHex);
        try
        {
            return new MudColor(CssNamedColors.ToHex(color, FallbackHex));
        }
        catch
        {
            return new MudColor(FallbackHex);
        }
    }

    public static string ToHexString(this MudColor color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
