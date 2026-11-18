using System.Globalization;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Core.Songs;

/// <summary>
/// Turns a song and its request into the text the wheel, the Now Playing panel, the played-song list and the
/// winner dialog show. A missing artist, title or requester shows as "Unknown".
/// </summary>
public static class SongDisplayText
{
    private const string UnknownValue = "Unknown";
    private const string NoDonation = "None";

    public static string BuildWheelLabel(SpinnerQueueItem song)
    {
        return $"{ValueOrUnknown(song.Song.Artist)} - {ValueOrUnknown(song.Song.Title)} ({GetPrimaryRequester(song)})";
    }

    public static string CreateNowPlayingText(SpinnerQueueItem song, SpinnerNowPlayingConfig nowPlaying)
    {
        var fields = nowPlaying.Fields is { Length: > 0 }
            ? nowPlaying.Fields
            : SongFieldNames.CreateDefaultSelection();
        return CreateTextForFields(song, fields, nowPlaying.Separator, nowPlaying.ShowLabels);
    }

    internal static string GetPrimaryRequester(SpinnerQueueItem song)
    {
        return ValueOrUnknown(song.Requests.FirstOrDefault()?.Name);
    }

    internal static string FormatDonation(SpinnerQueueItem song)
    {
        return FormatDonationFromRequest(song.Requests.FirstOrDefault(), NoDonation);
    }

    /// <summary>A queue entry's value for one <see cref="SongFieldNames"/> field; "None" for no donation.</summary>
    internal static string GetFieldValue(SpinnerQueueItem song, string field)
    {
        return GetFieldValue(song.Song, song.Requests, field, NoDonation);
    }

    /// <summary>A played song's value for one <see cref="SongFieldNames"/> field; empty for no donation.</summary>
    internal static string GetFieldValue(PlayHistoryItem item, string field)
    {
        return GetFieldValue(item.Song, item.Requests, field, donationFallback: "");
    }

    internal static string CreateTextForFields(
        SpinnerQueueItem song,
        IEnumerable<string> fields,
        string? separator = null,
        bool showLabels = true)
    {
        var parts = fields
            .Select(field => SongFieldNames.TryNormalize(field, out var normalized) ? normalized : "")
            .Where(field => field.Length > 0)
            .Select(field => (field, value: GetFieldValue(song, field)))
            .Where(x => !string.IsNullOrEmpty(x.value))
            .Select(x => FormatField(x.field, x.value, showLabels));
        return string.Join(SongTextFormatting.NormalizeSeparator(separator), parts);
    }

    internal static string FormatField(string field, string value, bool showLabels)
    {
        return showLabels
            ? $"{FormatFieldLabel(field)}: {value}"
            : value;
    }

    internal static string FormatFieldLabel(string field)
    {
        return $"{char.ToUpperInvariant(field[0])}{field[1..]}";
    }

    private static string GetFieldValue(
        SpinnerSong? song,
        List<SpinnerRequest> requests,
        string field,
        string donationFallback)
    {
        if (!SongFieldNames.TryNormalize(field, out var normalizedField)) return "";

        var request = requests.FirstOrDefault();
        return normalizedField switch
        {
            SongFieldNames.Artist => ValueOrUnknown(song?.Artist),
            SongFieldNames.Title => ValueOrUnknown(song?.Title),
            SongFieldNames.Requester => ValueOrUnknown(request?.Name),
            SongFieldNames.Donation => FormatDonationFromRequest(request, donationFallback),
            _ => ""
        };
    }

    private static string ValueOrUnknown(string? value) => value is { Length: > 0 } ? value : UnknownValue;

    // The one place donations are formatted. The fallback differs by context: queue entries show "None",
    // played songs leave the field out (empty string).
    private static string FormatDonationFromRequest(SpinnerRequest? request, string fallback)
    {
        if (request is null) return fallback;
        var amount = request.DonationAmount ?? request.Amount;
        return amount.HasValue ? amount.Value.ToString(CultureInfo.InvariantCulture) : fallback;
    }
}
