using System.Globalization;

namespace SonglistSpinner.Core.StreamerSongList;

/// <summary>
/// StreamerSongList's id for one entry in a streamer's queue (<see cref="SpinnerQueueItem.QueueId"/>). It is
/// not the song's id, and is a different number from a <see cref="StreamerId"/>.
/// </summary>
/// <remarks>
/// The constructor rejects ids that are not positive. <c>default</c> still holds 0, so API calls check for it too.
/// </remarks>
public readonly record struct QueueEntryId
{
    public QueueEntryId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
