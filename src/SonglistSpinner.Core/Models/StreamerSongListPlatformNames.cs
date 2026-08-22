namespace SonglistSpinner.Core.Models;

public static class StreamerSongListPlatformNames
{
    public const string Twitch = "twitch";
    public const string YouTube = "youtube";
    public const string Kick = "kick";
    public const string None = "none";
    public const string Default = Twitch;

    private static readonly string[] SupportedValues = [Twitch, YouTube, Kick, None];

    public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

    public static bool TryNormalize(string? value, out string normalized)
    {
        var candidate = value?.Trim();
        foreach (var supported in SupportedValues)
        {
            if (!string.Equals(candidate, supported, StringComparison.OrdinalIgnoreCase)) continue;
            normalized = supported;
            return true;
        }

        normalized = "";
        return false;
    }

    public static string NormalizeOrDefault(string? value)
    {
        return TryNormalize(value, out var normalized) ? normalized : Default;
    }
}
