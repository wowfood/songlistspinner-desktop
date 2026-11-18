using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Simulator;

namespace SonglistSpinner.IntegrationTests;

/// <summary>The app's real StreamerSongList client and event source, pointed at a simulator.</summary>
internal static class SimulatorClients
{
    /// <summary>Long enough for a loopback round trip on a busy CI machine; never a wait the test relies on.</summary>
    public static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>The event source's reconnect delay, distinct from every other timer it starts.</summary>
    public static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(1);

    public static StreamerSongListApiClient CreateApiClient(
        StreamerSongListSimulator simulator,
        HttpClient http,
        TimeProvider time,
        string? token = null,
        int pageSize = 100) =>
        new(
            http,
            new FixedCredentialProvider(new StreamerSongListCredential(
                StreamerSongListCredentialKind.Streamer,
                token ?? simulator.AccessToken)),
            new StreamerSongListApiOptions { BaseAddress = simulator.ApiBaseAddress, PageSize = pageSize },
            time);

    /// <remarks>
    /// The source starts a receive-idle timer on <paramref name="time"/> for every message it reads. It is an
    /// hour here, so a test that advances the clock past <see cref="InitialReconnectDelay"/> never trips it.
    /// </remarks>
    public static CentrifugoStreamerSongListEventSource CreateEventSource(
        StreamerSongListSimulator simulator,
        TimeProvider time) =>
        new(
            new StreamerSongListEventsOptions
            {
                Endpoint = simulator.EventsEndpoint,
                ReceiveIdleTimeout = TimeSpan.FromHours(1),
                InitialReconnectDelay = InitialReconnectDelay,
                MaximumReconnectDelay = TimeSpan.FromSeconds(30)
            },
            time);

    private sealed class FixedCredentialProvider(StreamerSongListCredential credential)
        : IStreamerSongListCredentialProvider
    {
        public ValueTask<StreamerSongListCredential?> GetCredentialAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<StreamerSongListCredential?>(credential);
    }
}
