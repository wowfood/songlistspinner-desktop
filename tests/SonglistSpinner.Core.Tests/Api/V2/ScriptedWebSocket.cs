using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace SonglistSpinner.Core.Tests.Api.V2;

/// <summary>
/// A client WebSocket whose server side is a script: queued messages are received in order, and once the
/// script is closed (or the socket disposed) the next receive returns a close frame. Sent text is recorded.
/// </summary>
internal sealed class ScriptedWebSocket : WebSocket
{
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
    private readonly List<string> _sent = [];
    private byte[]? _receiving;
    private int _receivedBytes;
    private WebSocketState _state = WebSocketState.Open;

    /// <summary>A socket whose server accepts the connect command and both channel subscriptions.</summary>
    public static ScriptedWebSocket AcceptingSubscriptions(params string[] pushedDuringSetup)
    {
        var socket = new ScriptedWebSocket();
        socket.Push("""{"id":1,"connect":{}}""");
        foreach (var message in pushedDuringSetup)
            socket.Push(message);
        socket.Push("""{"id":2,"subscribe":{}}""");
        socket.Push("""{"id":3,"subscribe":{}}""");
        return socket;
    }

    public bool IsDisposed { get; private set; }

    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sent)
                return [.. _sent];
        }
    }

    public override WebSocketCloseStatus? CloseStatus => null;

    public override string? CloseStatusDescription => null;

    public override WebSocketState State => _state;

    public override string? SubProtocol => null;

    public void Push(string message) => _incoming.Writer.TryWrite(message);

    public void CloseFromServer() => _incoming.Writer.TryComplete();

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (_receiving is null)
        {
            if (!await _incoming.Reader.WaitToReadAsync(cancellationToken) ||
                !_incoming.Reader.TryRead(out var message))
            {
                _state = WebSocketState.CloseReceived;
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, null);
            }

            _receiving = Encoding.UTF8.GetBytes(message);
            _receivedBytes = 0;
        }

        var count = Math.Min(buffer.Count, _receiving.Length - _receivedBytes);
        _receiving.AsSpan(_receivedBytes, count).CopyTo(buffer.AsSpan());
        _receivedBytes += count;
        var endOfMessage = _receivedBytes == _receiving.Length;
        if (endOfMessage) _receiving = null;

        return new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage);
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        lock (_sent)
            _sent.Add(Encoding.UTF8.GetString(buffer));
        return Task.CompletedTask;
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort() => _state = WebSocketState.Aborted;

    public override void Dispose()
    {
        IsDisposed = true;
        _state = WebSocketState.Closed;
        // An abandoned receive ends with a close frame rather than faulting unobserved.
        _incoming.Writer.TryComplete();
    }
}
