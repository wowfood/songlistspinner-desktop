namespace SonglistSpinner.Core.Api.V2;

public sealed class StreamerSongListEventsOptions
{
    public static readonly Uri ProductionEndpoint =
        new("wss://events.streamersonglist.com/connection/websocket", UriKind.Absolute);

    public Uri Endpoint { get; init; } = ProductionEndpoint;
    public TimeSpan ReceiveIdleTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan InitialReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);
    public int MaximumMessageBytes { get; init; } = 1024 * 1024;
}
