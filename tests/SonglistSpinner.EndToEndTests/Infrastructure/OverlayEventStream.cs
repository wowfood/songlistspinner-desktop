using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>An OBS browser source's view of the overlay: the server-sent events the overlay server streams.</summary>
internal sealed class OverlayEventStream : IAsyncDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(EndToEnd.ExpectTimeoutMilliseconds);

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
    /// <exception cref="TimeoutException">None arrived within the expectation timeout.</exception>
    public Task<JsonElement> NextAsync(string name, CancellationToken cancellationToken) =>
        NextAsync(name, _ => true, cancellationToken, "any data");

    /// <summary>
    /// Reads events until the next one named <paramref name="name"/> whose data matches, skipping the others. The
    /// whole wait is bounded by the expectation timeout, like a Playwright expectation, so a missed event fails the
    /// test there rather than at the test's own timeout.
    /// </summary>
    /// <param name="matchDescription">The predicate's source text, named in the timeout's message.</param>
    /// <exception cref="TimeoutException">No matching event arrived within the expectation timeout.</exception>
    public async Task<JsonElement> NextAsync(
        string name,
        Func<JsonElement, bool> match,
        CancellationToken cancellationToken,
        [CallerArgumentExpression(nameof(match))] string matchDescription = "")
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        var skipped = new List<string>();
        try
        {
            while (true)
            {
                var overlayEvent = await _events.Reader.ReadAsync(limit.Token);
                if (overlayEvent.Name == name && match(overlayEvent.Data)) return overlayEvent.Data;
                skipped.Add(overlayEvent.Name);
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No overlay event \"{name}\" matching {matchDescription} arrived within {Limit.TotalSeconds} s; " +
                $"skipped [{string.Join(", ", skipped)}].",
                ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _reading;
        _response.Dispose();
        _http.Dispose();
        _stop.Dispose();
    }

    /// <summary>
    /// Never throws: a failure (a reset stream, an event that is not JSON) completes the channel with it, so a
    /// test waiting in <see cref="NextAsync(string, Func{JsonElement, bool}, CancellationToken)"/> fails at once
    /// with the cause as the inner exception instead of waiting out its timeout.
    /// </summary>
    private async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
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
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        _events.Writer.TryComplete(failure);
    }

    private sealed record OverlayEvent(string Name, JsonElement Data);
}
