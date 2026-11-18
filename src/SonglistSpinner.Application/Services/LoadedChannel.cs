using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// The StreamerSongList channel the session has loaded: the id its realtime events and Now Playing actions use,
/// and the name its queue is fetched by.
/// </summary>
public sealed record LoadedChannel(StreamerId Id, string Name);
