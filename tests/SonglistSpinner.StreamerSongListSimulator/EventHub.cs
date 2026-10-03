using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace SonglistSpinner.Simulator;

/// <summary>
/// A Centrifugo-compatible WebSocket endpoint speaking the JSON protocol the app's event source uses: one JSON
/// object per text frame; <c>{"id":n,"connect":{}}</c> and <c>{"id":n,"subscribe":{"channel":...}}</c> commands
/// answered with a reply carrying the same id; publications pushed as
/// <c>{"push":{"channel":...,"pub":{"data":{"type":...,"data":...}}}}</c>; and an empty object <c>{}</c> as the
/// application-level ping, which the client answers with <c>{}</c>.
/// </summary>
/// <remarks>
/// Only <c>streamer:{id}-queue</c> and <c>streamer:{id}-play_history</c> can be subscribed; a publication reaches
/// only the connections subscribed to its channel. Connections are anonymous, as in production.
/// </remarks>
internal sealed partial class EventHub
{
    private const int UnknownChannelCode = 102;
    private const int BadRequestCode = 107;
    private const int UnauthorizedCode = 101;

    private readonly ConcurrentDictionary<EventConnection, byte> _connections = new();

    public int ConnectionCount => _connections.Count;

    public async Task AcceptAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connection = new EventConnection(socket, context);
        _connections.TryAdd(connection, 0);
        try
        {
            while (await ReceiveTextAsync(socket, context.RequestAborted) is { } message)
                await HandleAsync(connection, message, context.RequestAborted);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
        {
            // The client went away or the simulator dropped the connection.
        }
        finally
        {
            _connections.TryRemove(connection, out _);
            connection.Dispose();
        }
    }

    /// <summary>Sends a publication to every connection subscribed to <paramref name="channel"/>.</summary>
    public async Task PublishAsync(string channel, string eventType, int subjectId)
    {
        var message = JsonSerializer.Serialize(new
        {
            push = new { channel, pub = new { data = new { type = eventType, data = new { id = subjectId } } } }
        });

        foreach (var connection in _connections.Keys.Where(connection => connection.IsSubscribedTo(channel)))
            await connection.TrySendAsync(message);
    }

    /// <summary>Aborts every connection without a close handshake, as a network failure would.</summary>
    public void DropAll()
    {
        foreach (var connection in _connections.Keys)
            connection.Abort();
    }

    /// <summary>Sends the application ping to every connection; completes when each has answered.</summary>
    public Task PingAllAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_connections.Keys.Select(connection => connection.PingAsync(cancellationToken)));

    private static async Task HandleAsync(EventConnection connection, string message, CancellationToken cancellationToken)
    {
        if (message.AsSpan().Trim().SequenceEqual("{}"))
        {
            connection.ReceivedPong();
            return;
        }

        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var idProperty) || !idProperty.TryGetInt32(out var id))
            return;

        if (root.TryGetProperty("connect", out _))
        {
            connection.IsConnected = true;
            await connection.SendAsync(
                JsonSerializer.Serialize(new
                {
                    id,
                    connect = new { client = Guid.NewGuid().ToString(), version = "simulator", ping = 25, pong = true }
                }),
                cancellationToken);
            return;
        }

        if (root.TryGetProperty("subscribe", out var subscribe))
        {
            var channel = subscribe.TryGetProperty("channel", out var channelProperty)
                ? channelProperty.GetString()
                : null;
            if (!connection.IsConnected)
                await connection.SendAsync(Error(id, UnauthorizedCode, "unauthorized"), cancellationToken);
            else if (channel is null || !StreamerChannelPattern().IsMatch(channel))
                await connection.SendAsync(Error(id, UnknownChannelCode, "unknown channel"), cancellationToken);
            else
            {
                connection.Subscribe(channel);
                await connection.SendAsync(JsonSerializer.Serialize(new { id, subscribe = new { } }), cancellationToken);
            }

            return;
        }

        await connection.SendAsync(Error(id, BadRequestCode, "bad request"), cancellationToken);
    }

    private static string Error(int id, int code, string message) =>
        JsonSerializer.Serialize(new { id, error = new { code, message } });

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    [GeneratedRegex(@"^streamer:[1-9][0-9]*-(queue|play_history)$")]
    private static partial Regex StreamerChannelPattern();

    private sealed class EventConnection(WebSocket socket, HttpContext context) : IDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly ConcurrentDictionary<string, byte> _channels = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<TaskCompletionSource> _pendingPongs = new();

        public bool IsConnected { get; set; }

        public bool IsSubscribedTo(string channel) => _channels.ContainsKey(channel);

        public void Subscribe(string channel) => _channels.TryAdd(channel, 0);

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        /// <summary>Sends unless the connection has already gone, which a publication does not care about.</summary>
        public async Task TrySendAsync(string message)
        {
            try
            {
                await SendAsync(message, context.RequestAborted);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or
                                           ObjectDisposedException)
            {
            }
        }

        public async Task PingAsync(CancellationToken cancellationToken)
        {
            var pong = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingPongs.Enqueue(pong);
            await SendAsync("{}", cancellationToken);
            await pong.Task.WaitAsync(cancellationToken);
        }

        public void ReceivedPong()
        {
            if (_pendingPongs.TryDequeue(out var pong)) pong.TrySetResult();
        }

        public void Abort() => context.Abort();

        public void Dispose() => _sendGate.Dispose();
    }
}
