using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.Winner;

namespace SonglistSpinner.Services;

/// <summary>
/// The song a spin landed on, as the winner dialog shows it. <see cref="QueuePosition"/> is null when the
/// dialog does not show positions or the current position could not be looked up.
/// </summary>
public sealed record SpinWinner(SpinnerQueueItem Song, WinnerDialogField[] Fields, int? QueuePosition);
