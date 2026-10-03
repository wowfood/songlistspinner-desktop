using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Services;

/// <summary>
/// The overlay state the Settings preview shows. The songs are fixed samples, so only the draft settings
/// change what the preview looks like, and the live OBS overlay is never touched.
/// </summary>
public static class SettingsPreview
{
    public const string PlaceholderChannel = "your-channel";

    private static readonly SpinnerQueueItem[] SampleSongs =
    [
        CreateSampleSong(1, "The Midnight", "Sunset", "mod_jane", 10),
        CreateSampleSong(2, "CHVRCHES", "Clearest Blue", "musicfan"),
        CreateSampleSong(3, "Daft Punk", "Digital Love", "alex"),
        CreateSampleSong(4, "Florence + The Machine", "Dog Days Are Over", "streamviewer")
    ];

    /// <summary>
    /// All four samples are on the wheel; the first three are the played list and the fourth is Now Playing.
    /// </summary>
    public static OverlayStatePayload CreatePayload(SpinnerConfig config, string defaultStreamerName)
    {
        var playedSongs = SampleSongs.Take(3).ToArray();
        return new OverlayStatePayload(
            config,
            string.IsNullOrWhiteSpace(defaultStreamerName) ? PlaceholderChannel : defaultStreamerName.Trim(),
            SampleSongs.Select(song => new OverlayWheelItem(SongDisplayText.BuildWheelLabel(song))).ToArray(),
            PlayedSongList.CreateTexts(playedSongs, config),
            PlayedSongList.CreateFieldTable(playedSongs, config),
            SongDisplayText.CreateNowPlayingText(SampleSongs[3], config.NowPlaying),
            playedSongs.Length,
            SampleSongs.Length);
    }

    private static SpinnerQueueItem CreateSampleSong(
        int id,
        string artist,
        string title,
        string requester,
        decimal? donation = null)
    {
        return new SpinnerQueueItem
        {
            QueueId = id,
            Position = id,
            Song = new SpinnerSong { Id = id, Artist = artist, Title = title },
            Requests = [new SpinnerRequest { Name = requester, DonationAmount = donation }]
        };
    }
}
