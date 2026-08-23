namespace SonglistSpinner.Core.Models;

public sealed record PlayedSongFieldTable(
    string[] Headers,
    string Separator,
    PlayedSongFieldRow[] Rows);

public sealed record PlayedSongFieldRow(
    int? Number,
    string[] Values);
