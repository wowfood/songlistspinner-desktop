namespace SonglistSpinner.Core.Models;

public static class SongTextFormatting
{
    public const string Pipe = " | ";
    public const string Bullet = " • ";
    public const string MiddleDot = " · ";
    public const string Diamond = " ◆ ";
    public const string Star = " ★ ";
    public const string Slash = " / ";
    public const string Dash = " — ";
    public const string Arrow = " → ";
    public const string DefaultSeparator = Pipe;

    public static IReadOnlyList<string> Presets { get; } = Array.AsReadOnly<string>(
    [
        Pipe,
        Bullet,
        MiddleDot,
        Diamond,
        Star,
        Slash,
        Dash,
        Arrow
    ]);

    public static string NormalizeSeparator(string? separator) =>
        string.IsNullOrWhiteSpace(separator) ? DefaultSeparator : separator;
}
