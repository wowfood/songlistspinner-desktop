using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SonglistSpinner.Simulator;

/// <summary>
/// An in-memory StreamerSongList: the API v2 REST endpoints the app calls and a Centrifugo-compatible event
/// WebSocket, served over HTTP on 127.0.0.1. Every change, through the API or the scenario API, is published to
/// the subscribed event connections. It never contacts the real service.
/// </summary>
/// <remarks>
/// Each REST request passes, in order: a <see cref="HoldNextRequest">hold</see>, an
/// <see cref="FailNextRequests">injected failure</see> or <see cref="DropNextRequests">dropped connection</see>,
/// then the token check, then the endpoint. Every answered request is <see cref="Requests">recorded</see>,
/// failures included. The <c>/connection/websocket</c> event endpoint and the <c>/_simulator</c> endpoints (manual
/// testing and the <see cref="LatestReleasePath">update check</see>) are unauthenticated and not recorded there;
/// update checks have a log of their own, <see cref="LatestReleaseRequests"/>.
/// </remarks>
public sealed class StreamerSongListSimulator : IAsyncDisposable
{
    public const string EventsPath = "/connection/websocket";

    /// <summary>
    /// GitHub's "latest release" response for <see cref="LatestRelease"/>, or 404 (no release) while it is null.
    /// Relative to <see cref="ApiBaseAddress"/>.
    /// </summary>
    public const string LatestReleasePath = "_simulator/releases/latest";

    private const string ManualTestingPathPrefix = "/_simulator";
    private static readonly string[] AcceptedSchemes = ["Streamer", "User", "Bearer"];

    private readonly WebApplication _app;
    private readonly ChannelDirectory _channels;
    private readonly EventHub _events;
    private readonly RequestLog _requests = new();
    private readonly RequestLog _latestReleaseRequests = new();
    private readonly InjectedFaults _faults = new();
    private volatile SimulatedRelease? _latestRelease;

    private StreamerSongListSimulator(WebApplication app, StreamerSongListSimulatorOptions options)
    {
        _app = app;
        AccessToken = options.AccessToken;
        _events = new EventHub();
        _channels = new ChannelDirectory(new SimulatorContext(options.TimeProvider, _events));
    }

    /// <summary>Ends in a slash, so relative API paths resolve under it.</summary>
    public Uri ApiBaseAddress { get; private set; } = null!;

    public Uri EventsEndpoint { get; private set; } = null!;

    public string AccessToken { get; }

    /// <summary>Requests answered so far, oldest first.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests.Snapshot();

    public int EventConnectionCount => _events.ConnectionCount;

    /// <summary>
    /// The update checks answered at <see cref="LatestReleasePath"/> so far, oldest first, each recorded once its
    /// response was sent. Kept apart from <see cref="Requests"/>, which holds only StreamerSongList API calls.
    /// </summary>
    public IReadOnlyList<RecordedRequest> LatestReleaseRequests => _latestReleaseRequests.Snapshot();

    /// <summary>The release <see cref="LatestReleasePath"/> describes; null, the default, answers 404.</summary>
    public SimulatedRelease? LatestRelease
    {
        get => _latestRelease;
        set => _latestRelease = value;
    }

    /// <summary>
    /// While set, the event WebSocket refuses new connections with 503, so a client that loses its connection
    /// keeps reconnecting until this is cleared. Connections already open are not affected; see
    /// <see cref="DropEventConnections"/>.
    /// </summary>
    public bool RejectEventConnections
    {
        get => _events.RejectingConnections;
        set => _events.RejectingConnections = value;
    }

    public static async Task<StreamerSongListSimulator> StartAsync(
        StreamerSongListSimulatorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StreamerSongListSimulatorOptions();
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(StreamerSongListSimulator).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        // Listen overrides ASPNETCORE_URLS and any other configured address.
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, options.Port));
        if (!options.LogToConsole) builder.Logging.ClearProviders();

        var app = builder.Build();
        var simulator = new StreamerSongListSimulator(app, options);
        simulator.Configure();
        await app.StartAsync(cancellationToken);

        var address = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.First();
        var port = new Uri(address).Port;
        simulator.ApiBaseAddress = new Uri($"http://127.0.0.1:{port}/");
        simulator.EventsEndpoint = new Uri($"ws://127.0.0.1:{port}{EventsPath}");
        return simulator;
    }

    /// <summary>Adds a channel. Without <paramref name="streamerId"/>, ids are numbered from 1001.</summary>
    public SimulatedChannel AddChannel(string name, string platform = "twitch", int? streamerId = null) =>
        _channels.Add(name, platform, streamerId);

    /// <summary>
    /// Answers the next <paramref name="count"/> requests to <paramref name="method"/> <paramref name="path"/>
    /// (for example <c>GET /queue</c>) with <paramref name="statusCode"/> and a problem body whose
    /// <c>detail</c> is <paramref name="detail"/>, before the token is checked.
    /// </summary>
    public void FailNextRequests(HttpMethod method, string path, HttpStatusCode statusCode, string detail, int count = 1) =>
        _faults.Fail(method.Method, path, (int)statusCode, detail, count);

    /// <summary>
    /// Aborts the connection of the next <paramref name="count"/> requests to <paramref name="method"/>
    /// <paramref name="path"/> without answering, as a network failure would. They are recorded with
    /// <see cref="RecordedRequest.ConnectionAborted"/> set.
    /// </summary>
    public void DropNextRequests(HttpMethod method, string path, int count = 1) =>
        _faults.Abort(method.Method, path, count);

    /// <summary>Holds the next request to <paramref name="method"/> <paramref name="path"/> until released.</summary>
    public RequestHold HoldNextRequest(HttpMethod method, string path) => _faults.Hold(method.Method, path);

    /// <summary>Completes with the next answered request that matches; requests already answered do not count.</summary>
    public Task<RecordedRequest> WaitForRequestAsync(
        Func<RecordedRequest, bool> match,
        CancellationToken cancellationToken = default) =>
        _requests.WaitForNextAsync(match, cancellationToken);

    /// <summary>
    /// Completes with the oldest matching request in <see cref="Requests"/>, or with the next one answered when
    /// there is none yet. After <see cref="Reset"/> that is the first matching request since the reset, so a test
    /// can act first and wait afterwards.
    /// </summary>
    public Task<RecordedRequest> WaitForFirstRequestAsync(
        Func<RecordedRequest, bool> match,
        CancellationToken cancellationToken = default) =>
        _requests.WaitForFirstAsync(match, cancellationToken);

    /// <summary>
    /// Completes with the oldest matching update check in <see cref="LatestReleaseRequests"/>, or with the next one
    /// answered when there is none yet, so a test can tell when the app has its answer.
    /// </summary>
    public Task<RecordedRequest> WaitForFirstLatestReleaseRequestAsync(
        Func<RecordedRequest, bool> match,
        CancellationToken cancellationToken = default) =>
        _latestReleaseRequests.WaitForFirstAsync(match, cancellationToken);

    /// <summary>The answered requests to <paramref name="method"/> <paramref name="path"/>, oldest first.</summary>
    public IReadOnlyList<RecordedRequest> RequestsTo(HttpMethod method, string path) =>
        Requests.Where(request =>
                string.Equals(request.Method, method.Method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.Path, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    /// <summary>
    /// Returns the simulator to its state just after <see cref="StartAsync"/>, on the same address and token: no
    /// channels, no queued faults or holds (requests held now are released), an empty request log (pending
    /// waiters are cancelled), no release, event connections accepted, and every open event connection dropped.
    /// Queue, history and song ids keep counting up, so an id from before the reset never names a new entry.
    /// </summary>
    public void Reset()
    {
        _events.RejectingConnections = false;
        _events.DropAll();
        _channels.Clear();
        _latestRelease = null;
        _requests.Clear();
        _latestReleaseRequests.Clear();
        // Last: a request this releases runs on at once, and must find the channels and the log already emptied.
        _faults.Clear();
    }

    /// <summary>Aborts every event connection without a close handshake, as a network failure would.</summary>
    public void DropEventConnections() => _events.DropAll();

    /// <summary>Sends Centrifugo's application ping to every event connection; completes once each answers.</summary>
    public Task PingEventConnectionsAsync(CancellationToken cancellationToken = default) =>
        _events.PingAllAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        // An open WebSocket would otherwise hold shutdown until the host's timeout.
        _events.DropAll();
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(stopTimeout.Token);
        await _app.DisposeAsync();
    }

    private void Configure()
    {
        _app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.Zero });
        _app.Use(async (context, next) =>
        {
            if (IsApiRequest(context.Request)) await AnswerApiRequestAsync(context, next);
            else await next(context);
        });

        _app.Map(EventsPath, (RequestDelegate)_events.AcceptAsync);
        ApiEndpoints.Map(_app, _channels);
        MapManualTestingEndpoints();
    }

    private async Task AnswerApiRequestAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        var aborted = false;
        try
        {
            if (_faults.TakeHold(request.Method, request.Path) is { } hold)
                await hold.WaitAsync(context.RequestAborted);

            if (_faults.TakeFailure(request.Method, request.Path) is { } failure)
            {
                if (failure.AbortConnection)
                {
                    aborted = true;
                    context.Abort();
                }
                else
                {
                    await WireFormat.Problem(failure.StatusCode, failure.Detail).ExecuteAsync(context);
                }
            }
            else if (!IsAuthorized(request, out var authorizationProblem))
                await WireFormat.Problem(StatusCodes.Status401Unauthorized, authorizationProblem).ExecuteAsync(context);
            else
                await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client gave up on a held request.
        }
        finally
        {
            _requests.Record(new RecordedRequest(
                request.Method,
                request.Path,
                request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal),
                request.Headers.Authorization.Count == 0 ? null : request.Headers.Authorization.ToString(),
                request.Headers.TryGetValue("Client-Id", out var clientId) ? clientId.ToString() : null,
                context.Response.StatusCode,
                aborted));
        }
    }

    private bool IsAuthorized(HttpRequest request, out string problem)
    {
        var header = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            problem = "authorization header is required";
            return false;
        }

        var parts = header.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !AcceptedSchemes.Contains(parts[0], StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(parts[1], AccessToken, StringComparison.Ordinal))
        {
            problem = "invalid access token";
            return false;
        }

        problem = "";
        return true;
    }

    private static bool IsApiRequest(HttpRequest request) =>
        !request.Path.StartsWithSegments(EventsPath, StringComparison.Ordinal) &&
        !request.Path.StartsWithSegments(ManualTestingPathPrefix, StringComparison.Ordinal);

    /// <summary>Unauthenticated helpers for a developer driving the running simulator by hand.</summary>
    private void MapManualTestingEndpoints()
    {
        _app.MapPost($"{ManualTestingPathPrefix}/requests", async (HttpRequest request) =>
        {
            var streamerIdValue = request.Query["streamer_id"].ToString();
            if (!int.TryParse(streamerIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var streamerId) ||
                _channels.Find(streamerId) is not { } channel)
                return Results.NotFound();

            var entry = await channel.RequestSongAsync(
                request.Query["artist"].ToString() is { Length: > 0 } artist ? artist : "Simulated Artist",
                request.Query["title"].ToString() is { Length: > 0 } title ? title : "Simulated Song",
                request.Query["requester"].ToString() is { Length: > 0 } requester ? requester : "viewer");
            return Results.Json(new { queueId = entry.QueueId }, statusCode: StatusCodes.Status201Created);
        });

        _app.MapPost($"{ManualTestingPathPrefix}/events/drop", () =>
        {
            _events.DropAll();
            return Results.NoContent();
        });

        _app.MapGet($"/{LatestReleasePath}", (HttpContext context) =>
        {
            context.Response.OnCompleted(() =>
            {
                _latestReleaseRequests.Record(new RecordedRequest(
                    context.Request.Method,
                    context.Request.Path,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    Authorization: null,
                    ClientId: null,
                    context.Response.StatusCode));
                return Task.CompletedTask;
            });

            return _latestRelease is { } release
                ? Results.Json(new
                {
                    tag_name = release.Tag,
                    html_url = release.HtmlUrl.ToString(),
                    draft = release.Draft,
                    prerelease = release.Prerelease,
                    published_at = release.PublishedAt
                })
                : Results.NotFound();
        });
    }
}
