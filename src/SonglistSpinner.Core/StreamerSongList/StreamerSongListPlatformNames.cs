using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Core.StreamerSongList;

public static class StreamerSongListPlatformNames
{
    public const string Twitch = "twitch";
    public const string YouTube = "youtube";
    public const string Kick = "kick";
    public const string None = "none";
    public const string Default = Twitch;

    private static readonly string[] SupportedValues = [Twitch, YouTube, Kick, None];

    public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

    public static bool TryNormalize(string? value, out string normalized) =>
        CanonicalValue.TryNormalize(value, SupportedValues, out normalized);

    public static string NormalizeOrDefault(string? value)
    {
        return TryNormalize(value, out var normalized) ? normalized : Default;
    }
}
