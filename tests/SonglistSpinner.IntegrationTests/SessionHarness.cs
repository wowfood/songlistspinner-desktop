using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Core.Winner;
using SonglistSpinner.Services;
using SonglistSpinner.Simulator;

namespace SonglistSpinner.IntegrationTests;

/// <summary>
/// The app's session services wired as <c>AddStreamerSession</c> wires them, over the real client and event
/// source talking to a simulator.
/// </summary>
/// <remarks>
/// The session runs its debounce and retry delays on <c>sessionTime</c>; the client and event source run on
/// <c>apiTime</c>, so a test tracking the session's timers sees only those.
/// </remarks>
internal sealed class SessionHarness : IAsyncDisposable
{
    private readonly HttpClient _http = new();

    public SessionHarness(StreamerSongListSimulator simulator, TimeProvider apiTime, TimeProvider sessionTime)
    {
        Client = SimulatorClients.CreateApiClient(simulator, _http, apiTime);
        Session = new StreamerSessionService(
            Client,
            SimulatorClients.CreateEventSource(simulator, apiTime),
            new OverlayStateService(),
            sessionTime);
        Loader = new ChannelLoader(Client, Session);
        WinnerActions = new WinnerActionService(Client, new NowPlayingTransitionService(Client), Session);
    }

    public StreamerSongListApiClient Client { get; }

    public StreamerSessionService Session { get; }

    public ChannelLoader Loader { get; }

    public WinnerActionService WinnerActions { get; }

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        WinnerActions.Dispose();
        _http.Dispose();
    }
}
