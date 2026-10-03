using MudBlazor.Utilities;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Extensions;

public static class ColorExtensions
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

    public static MudColor ToMudColorWithAlpha(this string? hex, double alpha)
    {
        var c = hex.ToMudColor();
        return new MudColor(c.R, c.G, c.B, (byte)(alpha * 255));
    }

    public static string ToHexString(this MudColor color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
