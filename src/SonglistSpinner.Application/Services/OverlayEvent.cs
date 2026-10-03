using System.Diagnostics.CodeAnalysis;

namespace SonglistSpinner.Services;

/// <summary>
/// One message for a connected overlay: a named event with its JSON payload, or a keep-alive that carries
/// nothing and only shows the connection is still open. <see cref="OverlayServerSentEvents"/> frames it
/// for the wire.
/// </summary>
public sealed class OverlayEvent
{
    private OverlayEvent(string? name, string? data)
    {
        Name = name;
        Data = data;
    }

    public static OverlayEvent KeepAlive { get; } = new(null, null);

    /// <summary>The event name, one of <see cref="OverlayEventNames"/>; null for a keep-alive.</summary>
    public string? Name { get; }

    /// <summary>The event's JSON payload; null for a keep-alive.</summary>
    public string? Data { get; }

    [MemberNotNullWhen(false, nameof(Name), nameof(Data))]
    public bool IsKeepAlive => Name is null;

    public static OverlayEvent Named(string name, string data) => new(name, data);
}
