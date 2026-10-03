namespace SonglistSpinner.Core.Settings;

// Colour pickers only accept #rrggbb, but saved colours may be CSS names (the pointer defaults to "wheat").
public static class CssNamedColors
{
    // Returns the hex form of a known colour name, a value that is already hex unchanged, and
    // fallbackHex for anything else.
    public static string ToHex(string color, string fallbackHex)
    {
        ArgumentNullException.ThrowIfNull(color);

        return color.ToLowerInvariant() switch
        {
            "wheat" => "#f5deb3",
            "white" => "#ffffff",
            "black" => "#000000",
            "red" => "#ff0000",
            "green" => "#008000",
            "blue" => "#0000ff",
            "yellow" => "#ffff00",
            "orange" => "#ffa500",
            "purple" => "#800080",
            "pink" => "#ffc0cb",
            "gray" or "grey" => "#808080",
            _ => color.StartsWith('#') ? color : fallbackHex
        };
    }
}
