using System.Text.Json;
using System.Threading.Channels;

namespace SonglistSpinner.EndToEndTests;

/// <summary>An OBS browser source's view of the overlay: the server-sent events the overlay server streams.</summary>
internal sealed class OverlayEventStream : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly HttpResponseMessage _response;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<OverlayEvent> _events = Channel.CreateUnbounded<OverlayEvent>();
    private readonly Task _reading;

    private OverlayEventStream(HttpClient http, HttpResponseMessage response, Stream stream)
    {
        _http = http;
        _response = response;
        _reading = ReadAsync(stream, _stop.Token);
    }

    public static async Task<OverlayEventStream> ConnectAsync(Uri eventsUri, CancellationToken cancellationToken)
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            var response = await http.GetAsync(eventsUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return new OverlayEventStream(http, response, await response.Content.ReadAsStreamAsync(cancellationToken));
        }
        catch
        {
            http.Dispose();
            throw;
        }
    }

    /// <summary>Reads events until the next one named <paramref name="name"/>, skipping the others.</summary>
    public Task<JsonElement> NextAsync(string name, CancellationToken cancellationToken) =>
        NextAsync(name, _ => true, cancellationToken);

    /// <summary>
    /// Reads events until the next one named <paramref name="name"/> whose data matches, skipping the others.
    /// </summary>
    public async Task<JsonElement> NextAsync(
        string name,
        Func<JsonElement, bool> match,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var overlayEvent = await _events.Reader.ReadAsync(cancellationToken);
            if (overlayEvent.Name == name && match(overlayEvent.Data)) return overlayEvent.Data;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await _reading;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or HttpRequestException)
        {
            // Stopping the read ends the stream.
        }

        _response.Dispose();
        _http.Dispose();
        _stop.Dispose();
    }

    private async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        string? name = null;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal) && name is not null)
            {
                using var data = JsonDocument.Parse(line["data: ".Length..]);
                await _events.Writer.WriteAsync(new OverlayEvent(name, data.RootElement.Clone()), cancellationToken);
                name = null;
            }
        }

        _events.Writer.TryComplete();
    }

    private sealed record OverlayEvent(string Name, JsonElement Data);
}
