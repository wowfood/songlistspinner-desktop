namespace SonglistSpinner.Core.PlayedSongs;

public sealed record PlayedSongFieldTable(
    string[] Headers,
    string Separator,
    PlayedSongFieldRow[] Rows);
