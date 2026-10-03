namespace SonglistSpinner.Simulator;

/// <summary>
/// A REST request the simulator answered, recorded once its response was produced: by then any state it read or
/// changed is settled. <see cref="Query"/> holds each parameter's first value, unescaped.
/// </summary>
public sealed record RecordedRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    string? Authorization,
    string? ClientId,
    int StatusCode);
