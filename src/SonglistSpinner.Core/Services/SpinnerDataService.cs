using System.Globalization;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

public static class SpinnerDataService
{
    // Shown in place of a missing artist, title or requester name.
    private const string UnknownValue = "Unknown";
    private const string NoDonation = "None";

    public static bool SongMatchesPlayed(SpinnerQueueItem queueItem, PlayHistoryItem playedItem)
    {
        var q = queueItem.Song;
        var p = playedItem.Song;
        if (q is null || p is null) return false;
        if (q.Id.HasValue && p.Id.HasValue) return q.Id == p.Id;
        return string.Equals(q.Artist, p.Artist, StringComparison.OrdinalIgnoreCase)
               && string.Equals(q.Title, p.Title, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetPrimaryRequester(SpinnerQueueItem song)
    {
        return ValueOrUnknown(song.Requests.FirstOrDefault()?.Name);
    }

    public static string BuildWheelLabel(SpinnerQueueItem song)
    {
        return $"{ValueOrUnknown(song.Song.Artist)} - {ValueOrUnknown(song.Song.Title)} ({GetPrimaryRequester(song)})";
    }

    public static string FormatDonation(SpinnerQueueItem song)
    {
        return FormatDonationFromRequest(song.Requests.FirstOrDefault(), NoDonation);
    }

    public static string GetSongFieldValue(SpinnerQueueItem song, string field)
    {
        return GetFieldValue(song.Song, song.Requests, field, NoDonation);
    }

    public static string CreateSongTextForFields(
        SpinnerQueueItem song,
        IEnumerable<string> fields,
        string? separator = null,
        bool showLabels = true)
    {
        var parts = fields
            .Select(field => SongFieldNames.TryNormalize(field, out var normalized) ? normalized : "")
            .Where(field => field.Length > 0)
            .Select(field => (field, value: GetSongFieldValue(song, field)))
            .Where(x => !string.IsNullOrEmpty(x.value))
            .Select(x => FormatSongField(x.field, x.value, showLabels));
        return string.Join(SongTextFormatting.NormalizeSeparator(separator), parts);
    }

    public static string CreateNowPlayingText(SpinnerQueueItem song, SpinnerNowPlayingConfig nowPlaying)
    {
        var fields = nowPlaying.Fields is { Length: > 0 }
            ? nowPlaying.Fields
            : SongFieldNames.CreateDefaultSelection();
        return CreateSongTextForFields(song, fields, nowPlaying.Separator, nowPlaying.ShowLabels);
    }

    public static string CreatePlayedSongText(SpinnerQueueItem song, SpinnerConfig config)
    {
        var fields = GetPlayedFields(config);
        return CreateSongTextForFields(
            song,
            fields,
            config.PlayedList.Separator,
            config.PlayedList.ShowLabels && !config.PlayedList.ShowFieldHeaders);
    }

    public static string[] CreatePlayedSongTexts(
        IReadOnlyList<SpinnerQueueItem> songs,
        SpinnerConfig config)
    {
        return CreatePlayedSongTexts(songs, config, song => CreatePlayedSongText(song, config));
    }

    public static PlayedSongFieldTable CreatePlayedSongFieldTable(
        IReadOnlyList<SpinnerQueueItem> songs,
        SpinnerConfig config)
    {
        return CreatePlayedSongFieldTable(songs, config, GetSongFieldValue);
    }

    public static string[] GetWinnerFields(SpinnerConfig config)
    {
        return SongFieldNames.NormalizeSelection(
            config.WinnerDialog.Fields,
            SongFieldNames.CreateWinnerDefaultSelection());
    }

    public static WinnerDialogField[] CreateWinnerDialogFields(
        SpinnerQueueItem song,
        SpinnerConfig config)
    {
        return GetWinnerFields(config)
            .Select(field => (field, value: GetSongFieldValue(song, field)))
            .Where(item => !string.IsNullOrEmpty(item.value))
            .Select(item => new WinnerDialogField(
                $"{char.ToUpperInvariant(item.field[0])}{item.field[1..]}",
                item.value))
            .ToArray();
    }

    public static int? FindQueuePosition(IEnumerable<SpinnerQueueItem> queue, int queueId)
    {
        if (queueId <= 0) return null;
        var item = queue.FirstOrDefault(candidate => candidate.QueueId == queueId);
        return item is { Position: > 0 } ? item.Position : null;
    }

    public static string CreatePlayedSongText(PlayHistoryItem item, SpinnerConfig config)
    {
        var fields = GetPlayedFields(config);
        var parts = fields
            .Select(field => (field, value: GetHistoryFieldValue(item, field)))
            .Where(x => !string.IsNullOrEmpty(x.value))
            .Select(x => FormatSongField(
                x.field,
                x.value,
                config.PlayedList.ShowLabels && !config.PlayedList.ShowFieldHeaders));
        return string.Join(SongTextFormatting.NormalizeSeparator(config.PlayedList.Separator), parts);
    }

    public static string[] CreatePlayedSongTexts(
        IReadOnlyList<PlayHistoryItem> songs,
        SpinnerConfig config)
    {
        return CreatePlayedSongTexts(songs, config, song => CreatePlayedSongText(song, config));
    }

    public static PlayedSongFieldTable CreatePlayedSongFieldTable(
        IReadOnlyList<PlayHistoryItem> songs,
        SpinnerConfig config)
    {
        return CreatePlayedSongFieldTable(songs, config, GetHistoryFieldValue);
    }

    private static string[] CreatePlayedSongTexts<T>(
        IReadOnlyList<T> songs,
        SpinnerConfig config,
        Func<T, string> createText)
    {
        return songs
            .Select((song, index) =>
            {
                var text = createText(song);
                var number = GetPlayedSongNumber(index, songs.Count, config);
                return number.HasValue ? $"{number}. {text}" : text;
            })
            .ToArray();
    }

    private static PlayedSongFieldTable CreatePlayedSongFieldTable<T>(
        IReadOnlyList<T> songs,
        SpinnerConfig config,
        Func<T, string, string> getFieldValue)
    {
        var fields = GetPlayedFields(config);
        var rows = songs
            .Select((song, index) => new PlayedSongFieldRow(
                GetPlayedSongNumber(index, songs.Count, config),
                fields.Select(field => getFieldValue(song, field)).ToArray()))
            .ToArray();

        return new PlayedSongFieldTable(
            fields.Select(FormatSongFieldLabel).ToArray(),
            SongTextFormatting.NormalizeSeparator(config.PlayedList.Separator),
            rows);
    }

    private static string[] GetPlayedFields(SpinnerConfig config)
    {
        return SongFieldNames.NormalizeSelection(config.SongList.Fields);
    }

    private static int? GetPlayedSongNumber(int index, int songCount, SpinnerConfig config)
    {
        if (!config.PlayedList.ShowNumbers) return null;

        var startsAtTop = string.Equals(
            config.PlayedList.NumberingStart,
            SpinnerSettingValues.PlayedListNumberingStarts.Top,
            StringComparison.OrdinalIgnoreCase);
        return startsAtTop ? index + 1 : songCount - index;
    }

    private static string GetHistoryFieldValue(PlayHistoryItem item, string field)
    {
        return GetFieldValue(item.Song, item.Requests, field, donationFallback: "");
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

    private static string FormatSongField(string field, string value, bool showLabels)
    {
        return showLabels
            ? $"{FormatSongFieldLabel(field)}: {value}"
            : value;
    }

    private static string FormatSongFieldLabel(string field)
    {
        return $"{char.ToUpperInvariant(field[0])}{field[1..]}";
    }

    // Single source of truth for donation formatting. fallback differs by context:
    // queue items show "None", history items omit the field entirely (empty string).
    private static string FormatDonationFromRequest(SpinnerRequest? request, string fallback)
    {
        if (request is null) return fallback;
        var amount = request.DonationAmount ?? request.Donation ?? request.Amount ?? request.Price;
        return amount.HasValue ? amount.Value.ToString(CultureInfo.InvariantCulture) : fallback;
    }

    // O(n×m) — acceptable for typical queue sizes (< 200 songs, < 100 played).
    // If scale demands it, replace the inner Any() with a HashSet lookup.
    public static List<SpinnerQueueItem> FilterAvailableSongs(
        IEnumerable<SpinnerQueueItem> all,
        IEnumerable<PlayHistoryItem> played,
        SpinnerConfig config)
    {
        if (!config.SongList.ExcludePlayedSongs) return all.ToList();
        var playedList = played.ToList();
        return all.Where(song => !playedList.Any(p => SongMatchesPlayed(song, p))).ToList();
    }
}
