using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

public static class StreamerSongListReferenceParser
{
    private static readonly IReadOnlyDictionary<string, string> RoutePlatforms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["t"] = "twitch",
            ["twitch"] = "twitch",
            ["y"] = "youtube",
            ["youtube"] = "youtube",
            ["k"] = "kick",
            ["kick"] = "kick",
            ["s"] = "none",
            ["streamersonglist"] = "none",
            ["none"] = "none"
        };

    public static bool TryParse(
        string? value,
        string fallbackPlatform,
        out StreamerSongListChannel channel,
        out string? error)
    {
        channel = new StreamerSongListChannel("");
        error = null;

        var input = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter a StreamerSongList URL or streamer name.";
            return false;
        }

        if (!TryNormalizePlatform(fallbackPlatform, out var platform))
        {
            error = $"Unsupported platform '{fallbackPlatform}'.";
            return false;
        }

        var path = input;
        var isAbsoluteUrl = false;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is not ("http" or "https"))
            {
                error = "The streamer URL must use http or https.";
                return false;
            }

            isAbsoluteUrl = true;
            path = uri.AbsolutePath;
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        string name;
        if (segments.Length >= 2 && RoutePlatforms.TryGetValue(segments[^2], out var routedPlatform))
        {
            platform = routedPlatform;
            name = segments[^1];
        }
        else if (!isAbsoluteUrl && segments.Length == 1)
        {
            name = segments[0];
        }
        else
        {
            error = "Use a streamer name or a URL ending in /t/name, /s/name, /k/name, or /y/name.";
            return false;
        }

        name = name.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "The streamer name is missing from the URL.";
            return false;
        }

        channel = new StreamerSongListChannel(name, platform);
        return true;
    }

    private static bool TryNormalizePlatform(string? value, out string platform)
    {
        platform = value?.Trim().ToLowerInvariant() ?? "";
        if (RoutePlatforms.TryGetValue(platform, out var normalized))
            platform = normalized;

        return platform is "twitch" or "youtube" or "kick" or "none";
    }
}
