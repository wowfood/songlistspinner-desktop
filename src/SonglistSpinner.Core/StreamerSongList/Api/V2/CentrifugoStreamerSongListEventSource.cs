using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SonglistSpinner.Core.StreamerSongList.Api.V2;

public sealed class CentrifugoStreamerSongListEventSource : IStreamerSongListEventSource
{
    private const int ReceiveBufferBytes = 16 * 1024;
    private readonly StreamerSongListEventsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> _connectAsync;
    private readonly ILogger<CentrifugoStreamerSongListEventSource> _logger;

    public CentrifugoStreamerSongListEventSource(
        StreamerSongListEventsOptions options,
        TimeProvider? timeProvider = null,
        ILogger<CentrifugoStreamerSongListEventSource>? logger = null)
        : this(options, timeProvider, ConnectClientWebSocketAsync, logger)
    {
    }

    // connectAsync opens a connected socket for each connection attempt, and the event source disposes it.
    // Tests pass a scripted socket here instead of connecting to a server.
    internal CentrifugoStreamerSongListEventSource(
        StreamerSongListEventsOptions options,
        TimeProvider? timeProvider,
        Func<Uri, CancellationToken, Task<WebSocket>> connectAsync,
        ILogger<CentrifugoStreamerSongListEventSource>? logger = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _connectAsync = connectAsync;
        _logger = logger ?? NullLogger<CentrifugoStreamerSongListEventSource>.Instance;

        if (!_options.Endpoint.IsAbsoluteUri ||
            _options.Endpoint.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("The StreamerSongList event endpoint must be an absolute WebSocket URI.",
                nameof(options));
        }

        if (_options.InitialReconnectDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The initial reconnect delay must be positive.");
        if (_options.MaximumReconnectDelay < _options.InitialReconnectDelay)
            throw new ArgumentOutOfRangeException(nameof(options),
                "The maximum reconnect delay must not be shorter than the initial delay.");
        if (_options.ReceiveIdleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The receive idle timeout must be positive.");
        if (_options.MaximumMessageBytes < ReceiveBufferBytes)
            throw new ArgumentOutOfRangeException(nameof(options),
                $"The maximum event message size must be at least {ReceiveBufferBytes} bytes.");
    }

    public async IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
        StreamerId streamerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The id type rejects non-positive values, but default(StreamerId) still holds 0.
        if (streamerId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(streamerId), "A positive streamer ID is required.");

        var reconnectAttempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            Exception? failure = null;
            var subscription = SubscribeOnceAsync(streamerId, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await subscription.MoveNextAsync();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    catch (Exception ex) when (ex is WebSocketException or InvalidDataException or IOException or
                                                   JsonException or InvalidOperationException or FormatException or
                                                   OverflowException)
                    {
                        failure = ex;
                        break;
                    }

                    if (!hasNext) break;

                    var notification = subscription.Current;
                    if (notification.Kind == StreamerSongListEventKind.Connected)
                        reconnectAttempt = 0;
                    yield return notification;
                }
            }
            finally
            {
                await subscription.DisposeAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var error = failure?.Message ?? "The StreamerSongList event connection closed.";
            var reconnectDelay = GetReconnectDelay(reconnectAttempt);
            // Reconnecting never gives up, so only losing a working connection is a warning; each further
            // failed attempt is retry detail.
            var level = reconnectAttempt == 0 ? LogLevel.Warning : LogLevel.Debug;
            reconnectAttempt++;
            _logger.Log(
                level,
                failure,
                "StreamerSongList events for streamer {StreamerId} disconnected; " +
                "reconnect attempt {ReconnectAttempt} in {ReconnectDelay}",
                streamerId,
                reconnectAttempt,
                reconnectDelay);
            yield return new StreamerSongListEvent(StreamerSongListEventKind.Reconnecting, Error: error);

            await Task.Delay(reconnectDelay, _timeProvider, cancellationToken);
        }
    }

    private async IAsyncEnumerable<StreamerSongListEvent> SubscribeOnceAsync(
        StreamerId streamerId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var socket = await _connectAsync(_options.Endpoint, cancellationToken);

        var received = new Queue<string>();
        var pending = new List<StreamerSongListEvent>();
        await SendCommandAsync(socket, CentrifugoProtocol.CreateConnectCommand(1), cancellationToken);
        await AwaitCommandReplyAsync(socket, received, 1, pending, cancellationToken);

        await SendCommandAsync(socket,
            CentrifugoProtocol.CreateSubscribeCommand(2, $"streamer:{streamerId}-queue"),
            cancellationToken);
        await AwaitCommandReplyAsync(socket, received, 2, pending, cancellationToken);

        await SendCommandAsync(socket,
            CentrifugoProtocol.CreateSubscribeCommand(3, $"streamer:{streamerId}-play_history"),
            cancellationToken);
        await AwaitCommandReplyAsync(socket, received, 3, pending, cancellationToken);

        yield return new StreamerSongListEvent(StreamerSongListEventKind.Connected);
        foreach (var notification in pending)
            yield return notification;

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveMessageAsync(socket, received, cancellationToken);
            if (message is null)
                throw new WebSocketException("The StreamerSongList event server closed the connection.");

            if (CentrifugoProtocol.IsApplicationPing(message))
            {
                await SendCommandAsync(socket, "{}", cancellationToken);
                continue;
            }

            if (CentrifugoProtocol.IsDisconnect(message))
                throw new WebSocketException("The StreamerSongList event server requested a reconnect.");

            if (CentrifugoProtocol.TryParseNotification(message, out var notification))
                yield return notification;
        }
    }

    private async Task AwaitCommandReplyAsync(
        WebSocket socket,
        Queue<string> received,
        int commandId,
        ICollection<StreamerSongListEvent> pending,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var message = await ReceiveMessageAsync(socket, received, cancellationToken);
            if (message is null)
                throw new WebSocketException("The StreamerSongList event server closed during setup.");

            if (CentrifugoProtocol.IsApplicationPing(message))
            {
                await SendCommandAsync(socket, "{}", cancellationToken);
                continue;
            }

            if (CentrifugoProtocol.IsDisconnect(message))
                throw new WebSocketException("The StreamerSongList event server rejected the connection.");

            if (CentrifugoProtocol.IsCommandReply(message, commandId, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error)) throw new WebSocketException(error);
                return;
            }

            if (CentrifugoProtocol.TryParseNotification(message, out var notification))
                pending.Add(notification);
        }
    }

    private static Task SendCommandAsync(
        WebSocket socket,
        string command,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(command);
        return socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    /// <summary>
    /// Returns the next protocol message, or null once the server closes. Centrifugo's JSON transport may batch
    /// several messages into one frame, one per line (JSON escapes newlines inside strings), so the rest of a
    /// frame waits in <paramref name="received"/> for the following calls.
    /// </summary>
    private async Task<string?> ReceiveMessageAsync(
        WebSocket socket,
        Queue<string> received,
        CancellationToken cancellationToken)
    {
        while (received.Count == 0)
        {
            var frame = await ReceiveTextAsync(socket, cancellationToken);
            if (frame is null) return null;

            foreach (var message in frame.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                received.Enqueue(message);
        }

        return received.Dequeue();
    }

    private async Task<string?> ReceiveTextAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferBytes);
        try
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                        .WaitAsync(_options.ReceiveIdleTimeout, _timeProvider, cancellationToken);
                }
                catch (TimeoutException)
                {
                    throw new WebSocketException(
                        $"No StreamerSongList event data was received for {_options.ReceiveIdleTimeout.TotalSeconds:0} seconds.");
                }
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException("StreamerSongList sent an unsupported binary event message.");

                await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
                if (message.Length > _options.MaximumMessageBytes)
                    throw new InvalidDataException("StreamerSongList sent an event message that was too large.");
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<WebSocket> ConnectClientWebSocketAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private TimeSpan GetReconnectDelay(int attempt)
    {
        var multiplier = Math.Pow(2, Math.Min(attempt, 10));
        var ticks = Math.Min(
            _options.InitialReconnectDelay.Ticks * multiplier,
            _options.MaximumReconnectDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }
}
