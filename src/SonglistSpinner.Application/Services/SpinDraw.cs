using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Services;

/// <summary>
/// The queue a spin was drawn from and the song it picked. The wheel shows <see cref="AvailableSongs"/>
/// in order, so <see cref="WinnerIndex"/> is also the winner's slot on the wheel.
/// </summary>
public sealed class SpinDraw
{
    internal SpinDraw(
        string streamer,
        SpinnerConfig config,
        IReadOnlyList<SpinnerQueueItem> availableSongs,
        PlayHistoryItem[] playedSongs,
        SpinnerQueueItem? nowPlaying,
        int? winnerIndex)
    {
        if (winnerIndex is { } index)
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)availableSongs.Count, nameof(winnerIndex));

        Streamer = streamer;
        Config = config;
        AvailableSongs = availableSongs;
        PlayedSongs = playedSongs;
        NowPlaying = nowPlaying;
        WinnerIndex = winnerIndex;
    }

    public string Streamer { get; }

    public SpinnerConfig Config { get; }

    public IReadOnlyList<SpinnerQueueItem> AvailableSongs { get; }

    public PlayHistoryItem[] PlayedSongs { get; }

    public SpinnerQueueItem? NowPlaying { get; }

    /// <summary>The winner's position in <see cref="AvailableSongs"/>, or null when no songs are left to spin.</summary>
    public int? WinnerIndex { get; }

    public SpinnerQueueItem? Winner => WinnerIndex is { } index ? AvailableSongs[index] : null;
}
