using System.Globalization;

namespace SonglistSpinner.Core.Models;

/// <summary>A panel background: an opaque colour plus the opacity the panel is drawn with.</summary>
public readonly record struct PanelBackgroundColor
{
    public const string DefaultHex = "#000000";
    public const double DefaultOpacity = 0.7;

    /// <param name="hex">A <c>#RRGGBB</c> colour, in either case; it is stored in upper case.</param>
    /// <param name="opacity">Clamped to 0..1; <see cref="double.NaN"/> becomes <see cref="DefaultOpacity"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="hex"/> is not a <c>#RRGGBB</c> colour.</exception>
    public PanelBackgroundColor(string hex, double opacity)
    {
        if (!TryParseHex(hex, out var canonicalHex, out _, out _, out _))
            throw new ArgumentException($"'{hex}' is not a #RRGGBB colour.", nameof(hex));

        Hex = canonicalHex;
        Opacity = ClampOpacity(opacity);
    }

    public string Hex { get; }

    public double Opacity { get; }

    public static PanelBackgroundColor Parse(string? value)
    {
        if (TryParseRgba(value, out var rgba)) return rgba;
        if (TryParseHex(value, out var hex, out _, out _, out _)) return new PanelBackgroundColor(hex, 1.0);
        return new PanelBackgroundColor(DefaultHex, 1.0);
    }

    public static string Resolve(string? inheritedBackground, double? opacityOverride)
    {
        var parsed = Parse(inheritedBackground);
        if (opacityOverride.HasValue) return parsed.WithOpacity(opacityOverride.Value).ToCss();
        return string.IsNullOrWhiteSpace(inheritedBackground) ? parsed.ToCss() : inheritedBackground;
    }

    public PanelBackgroundColor WithOpacity(double opacity)
    {
        return new PanelBackgroundColor(Hex ?? DefaultHex, opacity);
    }

    public string ToCss()
    {
        // Only default(PanelBackgroundColor) has no Hex; the constructor guarantees a parsable one otherwise.
        _ = TryParseHex(Hex ?? DefaultHex, out _, out var red, out var green, out var blue);
        var opacity = ClampOpacity(Opacity).ToString("F2", CultureInfo.InvariantCulture);
        return $"rgba({red},{green},{blue},{opacity})";
    }

    public static double ClampOpacity(double opacity)
    {
        if (double.IsNaN(opacity)) return DefaultOpacity;
        return Math.Clamp(opacity, 0.0, 1.0);
    }

    private static bool TryParseRgba(string? value, out PanelBackgroundColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(')'))
            return false;

        var parts = value[5..^1].Split(',');
        if (parts.Length != 4 ||
            !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var red) ||
            !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var green) ||
            !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue) ||
            !double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) ||
            !double.IsFinite(opacity) ||
            red is < 0 or > 255 ||
            green is < 0 or > 255 ||
            blue is < 0 or > 255 ||
            opacity is < 0 or > 1)
            return false;

        color = new PanelBackgroundColor($"#{red:X2}{green:X2}{blue:X2}", opacity);
        return true;
    }

    private static bool TryParseHex(
        string? value,
        out string hex,
        out int red,
        out int green,
        out int blue)
    {
        hex = DefaultHex;
        red = 0;
        green = 0;
        blue = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var candidate = value.Trim();
        if (candidate.Length != 7 || candidate[0] != '#' ||
            !int.TryParse(candidate[1..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red) ||
            !int.TryParse(candidate[3..5], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green) ||
            !int.TryParse(candidate[5..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue))
            return false;

        hex = $"#{red:X2}{green:X2}{blue:X2}";
        return true;
    }
}
