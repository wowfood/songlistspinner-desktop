using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SonglistSpinner.Simulator;

/// <summary>
/// The REST endpoints the app's API client calls (see <c>docs/API_V2.md</c>). Authentication, injected faults
/// and the request log are applied before these run, by <see cref="StreamerSongListSimulator"/>.
/// </summary>
/// <remarks>
/// Where the real API's behaviour is not documented and the client never depends on it, the simulator picks
/// one: mutations answer 204; promoting an entry while another is playing puts the old one back at the front of
/// the queue; completing Now Playing when nothing is playing is a 404; nothing is promoted automatically.
/// </remarks>
internal static class ApiEndpoints
{
    private static readonly string[] Platforms = ["twitch", "youtube", "kick", "none"];

    public static void Map(WebApplication app, ChannelDirectory channels)
    {
        app.MapGet("/streamers", (HttpRequest request) =>
        {
            var (channel, problem) = FindChannel(request.Query, channels);
            return problem ?? WireFormat.Json(WireFormat.Streamer(channel!));
        });

        app.MapGet("/queue", (HttpRequest request) =>
        {
            var (channel, problem) = FindChannel(request.Query, channels);
            if (problem is not null) return problem;

            var (upcoming, playing) = channel!.ReadQueue();
            return WireFormat.Json(WireFormat.Queue(upcoming, playing));
        });

        app.MapGet("/play_history", (HttpRequest request) =>
        {
            var errors = new List<WireFormat.ProblemError>();
            var query = ParsePlayHistoryQuery(request.Query, errors);
            var (channel, problem) = FindChannel(request.Query, channels, errors);
            if (problem is not null) return problem;

            var (page, total) = query.Apply(channel!.ReadHistory());
            return WireFormat.Json(WireFormat.PlayHistory(page, total));
        });

        app.MapPost("/queue/played", async (HttpRequest request) =>
        {
            if (request.Query.ContainsKey("queue_id"))
            {
                if (!TryParsePositive(request.Query["queue_id"], out var queueId))
                    return Invalid("query.queue_id", "must be a positive integer", request.Query["queue_id"]);

                var owner = channels.FindByQueueEntry(queueId);
                return owner is not null && await owner.TryMarkPlayedAsync(queueId)
                    ? Results.NoContent()
                    : WireFormat.Problem(StatusCodes.Status404NotFound, "queue entry not found");
            }

            if (request.Query["position"] != "playing")
                return Invalid("query.position", "queue_id or position=playing is required", request.Query["position"]);
            if (!TryParsePositive(request.Query["streamer_id"], out var streamerId))
                return Invalid("query.streamer_id", "must be a positive integer", request.Query["streamer_id"]);

            var channel = channels.Find(streamerId);
            if (channel is null) return WireFormat.Problem(StatusCodes.Status404NotFound, "streamer not found");
            return await channel.TryMarkNowPlayingPlayedAsync()
                ? Results.NoContent()
                : WireFormat.Problem(StatusCodes.Status404NotFound, "nothing is playing");
        });

        app.MapPost("/queue/{queueId:int}/play", async (int queueId) =>
        {
            var channel = channels.FindByQueueEntry(queueId);
            if (channel is null) return WireFormat.Problem(StatusCodes.Status404NotFound, "queue entry not found");
            if (channel.NowPlaying?.QueueId == queueId) return Results.NoContent();

            return await channel.TryPromoteAsync(queueId)
                ? Results.NoContent()
                : WireFormat.Problem(StatusCodes.Status404NotFound, "queue entry not found");
        });
    }

    /// <summary>Reads <c>limit</c>, <c>order_by</c>, <c>order_dir</c> and <c>played_after</c>, adding an error for each bad value.</summary>
    internal static PlayHistoryQuery ParsePlayHistoryQuery(IQueryCollection query, List<WireFormat.ProblemError> errors)
    {
        var limit = PlayHistoryQuery.MaximumLimit;
        if (query.TryGetValue("limit", out var limitValue))
        {
            if (!int.TryParse(limitValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit))
                errors.Add(new("query.limit", "must be an integer", limitValue));
            else if (limit < 1)
                errors.Add(new("query.limit", "must be greater than or equal to 1", limitValue));
            else if (limit > PlayHistoryQuery.MaximumLimit)
                errors.Add(new("query.limit", $"must be less than or equal to {PlayHistoryQuery.MaximumLimit}", limitValue));
        }

        if (query.TryGetValue("order_by", out var orderBy) && orderBy != "played_at")
            errors.Add(new("query.order_by", "only played_at is supported", orderBy));

        var newestFirst = true;
        if (query.TryGetValue("order_dir", out var orderDirection))
        {
            if (orderDirection == "asc") newestFirst = false;
            else if (orderDirection != "desc") errors.Add(new("query.order_dir", "must be asc or desc", orderDirection));
        }

        DateTimeOffset? playedAfter = null;
        if (query.TryGetValue("played_after", out var playedAfterValue))
        {
            if (DateTimeOffset.TryParse(playedAfterValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                playedAfter = parsed;
            else
                errors.Add(new("query.played_after", "must be an RFC 3339 date-time", playedAfterValue));
        }

        return new PlayHistoryQuery(playedAfter, newestFirst, limit);
    }

    private static (SimulatedChannel? Channel, IResult? Problem) FindChannel(
        IQueryCollection query,
        ChannelDirectory channels,
        List<WireFormat.ProblemError>? errors = null)
    {
        errors ??= [];
        string? name = query["streamer_name"];
        string? platform = query["platform"];
        if (string.IsNullOrWhiteSpace(name))
            errors.Add(new("query.streamer_name", "is required", name));
        if (platform is null || !Platforms.Contains(platform, StringComparer.Ordinal))
            errors.Add(new("query.platform", "must be one of twitch, youtube, kick, none", platform));
        if (errors.Count > 0)
            return (null, WireFormat.Problem(StatusCodes.Status422UnprocessableEntity, "validation failed", errors));

        var channel = channels.Find(name!, platform!);
        return channel is null
            ? (null, WireFormat.Problem(StatusCodes.Status404NotFound, "streamer not found"))
            : (channel, null);
    }

    private static IResult Invalid(string location, string message, string? value) =>
        WireFormat.Problem(StatusCodes.Status422UnprocessableEntity, "validation failed", [new(location, message, value)]);

    private static bool TryParsePositive(string? value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
}
