namespace SonglistSpinner.Core.Models;

/// <summary>A StreamerSongList channel, named the way the API looks it up: streamer name plus platform.</summary>
/// <remarks>
/// The name is never blank. The platform is not checked here: the API client rejects one that
/// <see cref="StreamerSongListPlatformNames"/> does not know, so a stored value is reported where it is used.
/// </remarks>
public sealed record StreamerSongListChannel
{
    public StreamerSongListChannel(string name, string platform = StreamerSongListPlatformNames.Default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A streamer name is required.", nameof(name));

        Name = name;
        Platform = platform;
    }

    public string Name { get; }

    public string Platform { get; }
}
