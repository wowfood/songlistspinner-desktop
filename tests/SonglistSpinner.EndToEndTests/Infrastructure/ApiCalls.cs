using System.Globalization;
using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// The StreamerSongList calls the app makes, as matches for the simulator's request log, so a test can wait for
/// or count exactly the call a user action should cause:
/// <c>await simulator.WaitForFirstRequestAsync(ApiCalls.MarkPlayed(entry.QueueId), cancellationToken)</c>.
/// </summary>
internal static class ApiCalls
{
    /// <summary><c>GET /streamers?streamer_name=&amp;platform=</c>: resolving the channel.</summary>
    public static Func<RecordedRequest, bool> ResolveStreamer(string name, string platform = "twitch") =>
        request => Is(request, "GET", "/streamers") && NamesChannel(request, name, platform);

    /// <summary><c>GET /queue?streamer_name=&amp;platform=</c>.</summary>
    public static Func<RecordedRequest, bool> FetchQueue(string name, string platform = "twitch") =>
        request => Is(request, "GET", "/queue") && NamesChannel(request, name, platform);

    /// <summary><c>GET /play_history?streamer_name=&amp;platform=&amp;...</c>.</summary>
    public static Func<RecordedRequest, bool> FetchPlayHistory(string name, string platform = "twitch") =>
        request => Is(request, "GET", "/play_history") && NamesChannel(request, name, platform);

    /// <summary><c>POST /queue/played?queue_id=</c>: the winner's Mark Played.</summary>
    public static Func<RecordedRequest, bool> MarkPlayed(int queueId) =>
        request => Is(request, "POST", "/queue/played") &&
                   request.Query.GetValueOrDefault("queue_id") == Invariant(queueId);

    /// <summary><c>POST /queue/played?position=playing&amp;streamer_id=</c>: the Now Playing panel's Mark Played.</summary>
    public static Func<RecordedRequest, bool> MarkNowPlayingPlayed(int streamerId) =>
        request => Is(request, "POST", "/queue/played") &&
                   request.Query.GetValueOrDefault("position") == "playing" &&
                   request.Query.GetValueOrDefault("streamer_id") == Invariant(streamerId);

    /// <summary><c>POST /queue/{queueId}/play</c>: Set Now Playing.</summary>
    public static Func<RecordedRequest, bool> SetNowPlaying(int queueId) =>
        request => Is(request, "POST", $"/queue/{Invariant(queueId)}/play");

    /// <summary>Any request that changes the queue or history (every POST).</summary>
    public static bool IsQueueChange(RecordedRequest request) =>
        string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);

    private static bool Is(RecordedRequest request, string method, string path) =>
        string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(request.Path, path, StringComparison.OrdinalIgnoreCase);

    private static bool NamesChannel(RecordedRequest request, string name, string platform) =>
        request.Query.GetValueOrDefault("streamer_name") == name &&
        request.Query.GetValueOrDefault("platform") == platform;

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
