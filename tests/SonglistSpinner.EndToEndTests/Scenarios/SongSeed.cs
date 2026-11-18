namespace SonglistSpinner.EndToEndTests.Scenarios;

/// <summary>
/// A request to seed: the song, the viewer who asked for it and what they tipped. The app shows a tip with the
/// invariant culture's formatting of the decimal, so 3.50m shows as <c>3.50</c>.
/// </summary>
internal sealed record SongSeed(string Artist, string Title, string Requester, decimal? Donation = null)
{
    /// <summary>The wheel's label for this request: <c>Artist - Title (Requester)</c>.</summary>
    public string WheelLabel => $"{Artist} - {Title} ({Requester})";
}
