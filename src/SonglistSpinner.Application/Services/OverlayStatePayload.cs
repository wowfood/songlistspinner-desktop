using SonglistSpinner.Core.PlayedSongs;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Services;

// The wheel, played list and Now Playing state that Overlay.html renders in updateSongs(). It is sent as
// the update_songs event, opens the init_state event, and drives the settings preview. Serialized as
// camelCase JSON, so the property names are the overlay's contract.
public record OverlayStatePayload(
    SpinnerConfig Config,
    string Streamer,
    IReadOnlyList<OverlayWheelItem> WheelItems,
    string[] PlayedTexts,
    PlayedSongFieldTable PlayedFieldTable,
    string? NowPlayingText,
    int PlayedCount,
    int AvailableCount);
