using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Api.V2;

public sealed class StreamerSongListApiClient : ISpinnerApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IStreamerSongListCredentialProvider _credentialProvider;
    private readonly StreamerSongListApiOptions _options;
    private readonly TimeProvider _timeProvider;

    public StreamerSongListApiClient(
        HttpClient http,
        IStreamerSongListCredentialProvider credentialProvider,
        StreamerSongListApiOptions options,
        TimeProvider? timeProvider = null)
    {
        _http = http;
        _credentialProvider = credentialProvider;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (!_options.BaseAddress.IsAbsoluteUri)
            throw new ArgumentException("The StreamerSongList API base address must be absolute.", nameof(options));
        if (_options.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options), "Page size must be between 1 and 100.");
    }

    public async Task<StreamerSongListStreamer> ResolveStreamerAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default)
    {
        var query = BuildChannelQuery(channel);
        var dto = await GetAsync<StreamerDetailsDto>($"streamers?{query}", cancellationToken);
        if (dto.Id <= 0)
            throw new StreamerSongListApiException("StreamerSongList returned an invalid streamer ID.");

        return new StreamerSongListStreamer(dto.Id, MapPlatforms(dto.Platforms));
    }

    public async Task<int> ResolveStreamerIdAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default)
    {
        return (await ResolveStreamerAsync(channel, cancellationToken)).Id;
    }

    public async Task<SpinnerQueueItem[]> FetchQueueAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default)
    {
        return (await FetchQueueSnapshotAsync(channel, cancellationToken)).Items;
    }

    public async Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
        StreamerSongListChannel channel,
        CancellationToken cancellationToken = default)
    {
        var query = BuildChannelQuery(channel);
        var dto = await GetAsync<QueueResponseDto>($"queue?{query}", cancellationToken);
        return new SpinnerQueueSnapshot
        {
            Items = (dto.Items ?? []).Select(MapQueueItem).ToArray(),
            Playing = dto.Playing is null ? null : MapQueueItem(dto.Playing)
        };
    }

    public async Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
        StreamerSongListChannel channel,
        string period = SpinnerSettingValues.PlayHistoryPeriods.Default,
        CancellationToken cancellationToken = default)
    {
        var query = $"{BuildChannelQuery(channel)}&limit={_options.PageSize}" +
                    "&order_by=played_at&order_dir=desc";
        var playedAfter = GetPlayedAfter(period);
        if (playedAfter.HasValue)
        {
            query += $"&played_after={Uri.EscapeDataString(playedAfter.Value.ToString("O", CultureInfo.InvariantCulture))}";
        }

        var dto = await GetAsync<PlayHistoryResponseDto>($"play_history?{query}", cancellationToken);
        return (dto.Items ?? []).Select(MapPlayHistoryItem).ToArray();
    }

    public Task MarkQueueItemAsPlayedAsync(int queueId, CancellationToken cancellationToken = default)
    {
        ValidateQueueId(queueId);
        return SendWithoutResponseAsync(HttpMethod.Post, $"queue/played?queue_id={queueId}", cancellationToken);
    }

    public Task MarkNowPlayingAsPlayedAsync(int streamerId, CancellationToken cancellationToken = default)
    {
        ValidateStreamerId(streamerId);
        return SendWithoutResponseAsync(
            HttpMethod.Post,
            $"queue/played?position=playing&streamer_id={streamerId}",
            cancellationToken);
    }

    public Task PromoteQueueItemToNowPlayingAsync(int queueId, CancellationToken cancellationToken = default)
    {
        ValidateQueueId(queueId);
        return SendWithoutResponseAsync(HttpMethod.Post, $"queue/{queueId}/play", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(relativeUrl));
        await AddCredentialAsync(request, cancellationToken);
        Trace.WriteLine($"[SonglistSpinner API] GET {request.RequestUri}");

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        Trace.WriteLine(
            $"[SonglistSpinner API] HTTP {(int)response.StatusCode} {response.StatusCode} for {request.RequestUri}");

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                   ?? throw new StreamerSongListApiException("StreamerSongList returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new StreamerSongListApiException(
                "StreamerSongList returned a response that does not match API v2.",
                response.StatusCode,
                ex);
        }
    }

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(relativeUrl));
        await AddCredentialAsync(request, cancellationToken);
        Trace.WriteLine($"[SonglistSpinner API] {method} {request.RequestUri}");

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        Trace.WriteLine(
            $"[SonglistSpinner API] HTTP {(int)response.StatusCode} {response.StatusCode} for {request.RequestUri}");
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);
    }

    private async ValueTask AddCredentialAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialProvider.GetCredentialAsync(cancellationToken);
        if (credential is null || string.IsNullOrWhiteSpace(credential.Token))
        {
            throw new StreamerSongListApiException(
                "StreamerSongList API access is not configured. Add an API token in Settings.");
        }

        var scheme = credential.Kind switch
        {
            StreamerSongListCredentialKind.OAuthBearer => StreamerSongListAuthenticationSchemes.Bearer,
            StreamerSongListCredentialKind.Streamer => StreamerSongListAuthenticationSchemes.Streamer,
            StreamerSongListCredentialKind.User => StreamerSongListAuthenticationSchemes.User,
            _ => throw new ArgumentOutOfRangeException(nameof(credential.Kind))
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(scheme, credential.Token.Trim());
        if (credential.Kind == StreamerSongListCredentialKind.OAuthBearer &&
            !string.IsNullOrWhiteSpace(credential.ClientId))
        {
            request.Headers.TryAddWithoutValidation("Client-Id", credential.ClientId.Trim());
        }
    }

    private Uri BuildUri(string relativeUrl)
    {
        var baseAddress = _options.BaseAddress.AbsoluteUri.TrimEnd('/') + "/";
        return new Uri(new Uri(baseAddress, UriKind.Absolute), relativeUrl);
    }

    private static string BuildChannelQuery(StreamerSongListChannel channel)
    {
        var name = channel.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A streamer name is required.", nameof(channel));
        if (!StreamerSongListPlatformNames.TryNormalize(channel.Platform, out var platform))
            throw new ArgumentException($"Unsupported StreamerSongList platform '{channel.Platform}'.", nameof(channel));

        return $"streamer_name={Uri.EscapeDataString(name)}&platform={Uri.EscapeDataString(platform)}";
    }

    private static void ValidateQueueId(int queueId)
    {
        if (queueId <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueId), "A positive queue entry ID is required.");
    }

    private static void ValidateStreamerId(int streamerId)
    {
        if (streamerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(streamerId), "A positive streamer ID is required.");
    }

    private DateTimeOffset? GetPlayedAfter(string period)
    {
        if (!SpinnerSettingValues.PlayHistoryPeriods.TryNormalize(period, out var normalizedPeriod))
            throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown play-history period.");

        var now = _timeProvider.GetUtcNow();
        return normalizedPeriod switch
        {
            SpinnerSettingValues.PlayHistoryPeriods.Day => now.AddDays(-1),
            SpinnerSettingValues.PlayHistoryPeriods.Week => now.AddDays(-7),
            SpinnerSettingValues.PlayHistoryPeriods.Month => now.AddMonths(-1),
            SpinnerSettingValues.PlayHistoryPeriods.All or
                SpinnerSettingValues.PlayHistoryPeriods.Stream => null,
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown play-history period.")
        };
    }

    private static SpinnerQueueItem MapQueueItem(QueueDetailsDto item)
    {
        var title = item.Song?.Title;
        return new SpinnerQueueItem
        {
            QueueId = item.Id,
            Position = item.Position,
            Song = new SpinnerSong
            {
                Id = item.SongId,
                Artist = item.Song?.Artist ?? "",
                Title = string.IsNullOrWhiteSpace(title) ? item.NonlistSong ?? "" : title
            },
            Requests = MapRequests(item.Requests)
        };
    }

    private static IReadOnlyList<StreamerSongListPlatformIdentity> MapPlatforms(StreamerPlatformsDto? platforms)
    {
        if (platforms is null) return [];

        var identities = new List<StreamerSongListPlatformIdentity>();
        AddPlatformIdentity(identities, StreamerSongListPlatformNames.Twitch, platforms.Twitch);
        AddPlatformIdentity(identities, StreamerSongListPlatformNames.YouTube, platforms.YouTube);
        AddPlatformIdentity(identities, StreamerSongListPlatformNames.Kick, platforms.Kick);
        AddPlatformIdentity(identities, StreamerSongListPlatformNames.None, platforms.None);
        return identities;
    }

    private static void AddPlatformIdentity(
        ICollection<StreamerSongListPlatformIdentity> identities,
        string platform,
        StreamerPlatformDto? identity)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Username)) return;
        identities.Add(new StreamerSongListPlatformIdentity(
            platform,
            identity.Username.Trim(),
            identity.PlatformId?.Trim() ?? ""));
    }

    private static PlayHistoryItem MapPlayHistoryItem(PlayHistoryDetailsDto item)
    {
        var requests = MapRequests(item.Requests);
        if (item.DonationAmount.HasValue)
        {
            if (requests.Count == 0)
                requests.Add(new SpinnerRequest { DonationAmount = item.DonationAmount });
            else if (!requests[0].Amount.HasValue)
                requests[0].DonationAmount = item.DonationAmount;
        }

        return new PlayHistoryItem
        {
            Song = item.Song is null
                ? null
                : new SpinnerSong
                {
                    Id = item.SongId,
                    Artist = item.Song.Artist ?? "",
                    Title = item.Song.Title ?? ""
                },
            Requests = requests
        };
    }

    private static List<SpinnerRequest> MapRequests(IEnumerable<RequestDto>? requests)
    {
        if (requests is null) return [];

        return requests.Select(request => new SpinnerRequest
        {
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? request.User?.Username ?? ""
                : request.Name,
            Amount = request.Amount
        }).ToList();
    }

    private static async Task<StreamerSongListApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "StreamerSongList rejected the configured API token.",
            HttpStatusCode.Forbidden => "The configured StreamerSongList token cannot access this channel.",
            HttpStatusCode.TooManyRequests => "StreamerSongList rate-limited the request. Try again shortly.",
            _ => $"StreamerSongList returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
        };

        var detail = await TryReadErrorDetailAsync(response.Content, cancellationToken);
        if (!string.IsNullOrWhiteSpace(detail)) message += $" {detail}";
        return new StreamerSongListApiException(message, response.StatusCode);
    }

    private static async Task<string?> TryReadErrorDetailAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body)) return null;
            using var document = JsonDocument.Parse(body);
            var details = new List<string>();
            foreach (var propertyName in new[] { "message", "detail", "error" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) details.Add(value);
                }
            }

            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    var location = error.TryGetProperty("location", out var locationProperty)
                        ? locationProperty.GetString()
                        : null;
                    var message = error.TryGetProperty("message", out var messageProperty)
                        ? messageProperty.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(message)) continue;
                    details.Add(string.IsNullOrWhiteSpace(location) ? message : $"{location}: {message}");
                }
            }

            var combined = string.Join(" ", details.Distinct(StringComparer.OrdinalIgnoreCase));
            return combined.Length > 400 ? combined[..400] : combined;
        }
        catch (JsonException)
        {
            // Error bodies are not guaranteed to be JSON.
        }

        return null;
    }
}
