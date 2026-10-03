using System.Globalization;

namespace SonglistSpinner.Core.Models;

/// <summary>
/// StreamerSongList's numeric id for a streamer. It names the realtime event channels and the streamer whose
/// Now Playing entry is completed, and is a different number from a <see cref="QueueEntryId"/>.
/// </summary>
/// <remarks>
/// The constructor rejects ids that are not positive. <c>default</c> still holds 0, so API calls check for it too.
/// </remarks>
public readonly record struct StreamerId
{
    public StreamerId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
