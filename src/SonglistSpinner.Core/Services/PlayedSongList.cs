using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

/// <summary>
/// Builds the played-song list: one line of text per song, or a table of field values when the list shows
/// field headers. The fields, separator, labels and numbering come from the played-list settings.
/// </summary>
public static class PlayedSongList
{
    public static string[] CreateTexts(IReadOnlyList<PlayHistoryItem> songs, SpinnerConfig config)
    {
        return CreateTexts(songs, config, song => CreateText(song, config));
    }

    /// <summary>The settings preview shows queue entries as if they had been played.</summary>
    public static string[] CreateTexts(IReadOnlyList<SpinnerQueueItem> songs, SpinnerConfig config)
    {
        return CreateTexts(songs, config, song => CreateText(song, config));
    }

    public static PlayedSongFieldTable CreateFieldTable(IReadOnlyList<PlayHistoryItem> songs, SpinnerConfig config)
    {
        return CreateFieldTable(songs, config, SongDisplayText.GetFieldValue);
    }

    public static PlayedSongFieldTable CreateFieldTable(IReadOnlyList<SpinnerQueueItem> songs, SpinnerConfig config)
    {
        return CreateFieldTable(songs, config, SongDisplayText.GetFieldValue);
    }

    internal static string CreateText(PlayHistoryItem item, SpinnerConfig config)
    {
        var parts = GetPlayedFields(config)
            .Select(field => (field, value: SongDisplayText.GetFieldValue(item, field)))
            .Where(x => !string.IsNullOrEmpty(x.value))
            .Select(x => SongDisplayText.FormatField(x.field, x.value, ShowsInlineLabels(config)));
        return string.Join(SongTextFormatting.NormalizeSeparator(config.PlayedList.Separator), parts);
    }

    internal static string CreateText(SpinnerQueueItem song, SpinnerConfig config)
    {
        return SongDisplayText.CreateTextForFields(
            song,
            GetPlayedFields(config),
            config.PlayedList.Separator,
            ShowsInlineLabels(config));
    }

    private static string[] CreateTexts<T>(
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

    private static PlayedSongFieldTable CreateFieldTable<T>(
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
            fields.Select(SongDisplayText.FormatFieldLabel).ToArray(),
            SongTextFormatting.NormalizeSeparator(config.PlayedList.Separator),
            rows);
    }

    // With a header row the labels are in the headers, so the lines leave them out.
    private static bool ShowsInlineLabels(SpinnerConfig config) =>
        config.PlayedList.ShowLabels && !config.PlayedList.ShowFieldHeaders;

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
}
