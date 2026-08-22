namespace SonglistSpinner.Core.Api.V2;

internal static class StreamerSongListAuthenticationSchemes
{
    public const string Bearer = "Bearer";
    public const string Streamer = "Streamer";
    public const string User = "User";
}

internal static class StreamerSongListEventTypes
{
    public const string NowPlayingUpdate = "now_playing_update";
    public const string QueueAdd = "queue_add";
    public const string QueueClear = "queue_clear";
    public const string QueueRemove = "queue_remove";
    public const string QueueReorder = "queue_reorder";
    public const string QueueUpdate = "queue_update";
    public const string PlayHistoryAdd = "play_history_add";
    public const string PlayHistoryRemove = "play_history_remove";

    public static IReadOnlyList<string> QueueChanges { get; } = Array.AsReadOnly<string>(
    [
        NowPlayingUpdate, QueueAdd, QueueClear, QueueRemove, QueueReorder, QueueUpdate
    ]);

    public static IReadOnlyList<string> PlayHistoryChanges { get; } = Array.AsReadOnly<string>(
    [
        PlayHistoryAdd, PlayHistoryRemove
    ]);
}
