using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

public sealed class GitHubReleaseUpdateChecker
{
    public static readonly Uri LatestReleaseEndpoint =
        new("https://api.github.com/repos/wowfood/songlistspinner-desktop/releases/latest");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly Uri _latestReleaseEndpoint;

    public GitHubReleaseUpdateChecker(HttpClient httpClient, Uri? latestReleaseEndpoint = null)
    {
        _httpClient = httpClient;
        _latestReleaseEndpoint = latestReleaseEndpoint ?? LatestReleaseEndpoint;
    }

    public async Task<ApplicationUpdateInfo?> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd($"SonglistSpinner-Desktop/{Normalize(currentVersion)}");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<LatestReleaseResponse>(content, JsonOptions,
            cancellationToken);
        if (release is null || release.Draft || release.Prerelease ||
            !TryParseReleaseVersion(release.TagName, out var releaseVersion) ||
            releaseVersion <= Normalize(currentVersion) ||
            !TryGetTrustedReleaseUri(release.HtmlUrl, out var releaseUri))
        {
            return null;
        }

        return new ApplicationUpdateInfo(releaseVersion, release.TagName, releaseUri, release.PublishedAt);
    }

    internal static bool TryParseReleaseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        var segments = value.Split('.', StringSplitOptions.TrimEntries);
        if (segments.Length != 3 ||
            !int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new Version(major, minor, patch);
        return true;
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build));
    }

    private static bool TryGetTrustedReleaseUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
            parsed.Scheme == Uri.UriSchemeHttps &&
            parsed.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            parsed.AbsolutePath.StartsWith("/wowfood/songlistspinner-desktop/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private sealed class LatestReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }
    }
}
