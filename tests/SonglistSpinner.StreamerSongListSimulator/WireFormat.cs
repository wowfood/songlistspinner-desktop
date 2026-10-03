using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace SonglistSpinner.Simulator;

/// <summary>
/// The StreamerSongList API v2 JSON the simulator returns, written from <c>docs/API_V2.md</c> and the response
/// fixtures in <c>StreamerSongListApiClientTests</c>. Errors are <c>application/problem+json</c> in the shape of
/// the 422 fixture there: <c>title</c>, <c>status</c>, <c>detail</c> and optional <c>errors</c>.
/// </summary>
internal static class WireFormat
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IResult Json(object body) => Results.Json(body, JsonOptions);

    public static IResult Problem(int statusCode, string detail, IReadOnlyList<ProblemError>? errors = null) =>
        Results.Json(
            new ProblemBody(ReasonPhrases.GetReasonPhrase(statusCode), statusCode, detail, errors),
            JsonOptions,
            "application/problem+json",
            statusCode);

    public static StreamerJson Streamer(SimulatedChannel channel)
    {
        var identity = new PlatformIdentityJson($"sim-{channel.StreamerId}", channel.Name);
        return new StreamerJson(
            channel.StreamerId,
            channel.Name,
            new Dictionary<string, PlatformIdentityJson> { [channel.Platform] = identity });
    }

    public static QueueJson Queue(SimulatedQueueEntry[] upcoming, SimulatedQueueEntry? playing) =>
        new(
            upcoming.Select((entry, index) => QueueEntry(entry, index + 1)).ToArray(),
            playing is null ? null : QueueEntry(playing, 0),
            upcoming.Length);

    public static PlayHistoryJson PlayHistory(IReadOnlyList<SimulatedPlayedSong> page, int total) =>
        new(
            page.Select(song => new PlayHistoryEntryJson(
                song.HistoryId,
                song.PlayedAt,
                song.DonationAmount,
                Requests(song.Requests),
                new SongJson(song.Artist, song.Title),
                song.SongId)).ToArray(),
            null,
            total);

    private static QueueEntryJson QueueEntry(SimulatedQueueEntry entry, int position) =>
        new(entry.QueueId, position, "", Requests(entry.Requests), new SongJson(entry.Artist, entry.Title), entry.SongId);

    // A signed-in viewer's request carries an empty name and the viewer under user, as in the client's fixtures.
    private static RequestJson[] Requests(IEnumerable<SimulatedRequest> requests) =>
        requests.Select(request => new RequestJson(request.Amount, "", new RequestUserJson(request.Requester)))
            .ToArray();

    internal sealed record ProblemBody(
        string Title,
        int Status,
        string Detail,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<ProblemError>? Errors);

    internal sealed record StreamerJson(int Id, string Name, Dictionary<string, PlatformIdentityJson> Platforms);

    internal sealed record PlatformIdentityJson(
        [property: JsonPropertyName("platformID")] string PlatformId,
        string Username);

    internal sealed record QueueJson(QueueEntryJson[] Items, QueueEntryJson? Playing, int Total);

    internal sealed record QueueEntryJson(
        int Id,
        int Position,
        string NonlistSong,
        RequestJson[] Requests,
        SongJson Song,
        int SongId);

    internal sealed record PlayHistoryJson(PlayHistoryEntryJson[] Items, string? Token, int Total);

    internal sealed record PlayHistoryEntryJson(
        int Id,
        DateTimeOffset PlayedAt,
        decimal? DonationAmount,
        RequestJson[] Requests,
        SongJson Song,
        int SongId);

    internal sealed record SongJson(string Artist, string Title);

    internal sealed record RequestJson(decimal? Amount, string Name, RequestUserJson User);

    internal sealed record RequestUserJson(string Username);

    /// <summary>One field error in a problem body: where it is (<c>query.limit</c>), what is wrong and the value sent.</summary>
    internal sealed record ProblemError(string Location, string Message, string? Value);
}
