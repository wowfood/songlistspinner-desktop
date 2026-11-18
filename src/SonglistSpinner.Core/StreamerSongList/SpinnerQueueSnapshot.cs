namespace SonglistSpinner.Core.StreamerSongList;

public sealed class SpinnerQueueSnapshot
{
    public SpinnerQueueItem[] Items { get; init; } = [];
    public SpinnerQueueItem? Playing { get; init; }
}
