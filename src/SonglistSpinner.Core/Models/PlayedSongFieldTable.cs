namespace SonglistSpinner.Core.Models;

public sealed record PlayedSongFieldTable(
    string[] Headers,
    string Separator,
    PlayedSongFieldRow[] Rows);
