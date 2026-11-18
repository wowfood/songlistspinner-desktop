namespace SonglistSpinner.Services;

/// <summary>
/// Frames overlay events as server-sent events, the text/event-stream format the overlay's EventSource reads.
/// </summary>
public static class OverlayServerSentEvents
{
    public static string Frame(OverlayEvent overlayEvent)
    {
        // A keep-alive is an SSE comment: EventSource ignores it, but it keeps quiet browser sources alive and
        // makes a dropped connection fail the next write.
        return overlayEvent.IsKeepAlive
            ? ": keep-alive\n\n"
            : $"event: {overlayEvent.Name}\ndata: {overlayEvent.Data}\n\n";
    }
}
