using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Services;

/// <summary>What the winner dialog shows about the song a spin picked.</summary>
public static class WinnerDialogContent
{
    /// <summary>
    /// The winner's values for the configured winner-dialog fields, in their configured order. Fields with no
    /// value are left out.
    /// </summary>
    public static WinnerDialogField[] CreateFields(SpinnerQueueItem song, SpinnerConfig config)
    {
        return SelectFieldNames(config)
            .Select(field => (field, value: SongDisplayText.GetFieldValue(song, field)))
            .Where(item => !string.IsNullOrEmpty(item.value))
            .Select(item => new WinnerDialogField(SongDisplayText.FormatFieldLabel(item.field), item.value))
            .ToArray();
    }

    /// <summary>
    /// The winner's position in the queue, or <c>null</c> when it has left the queue or has no position.
    /// </summary>
    public static int? FindQueuePosition(IEnumerable<SpinnerQueueItem> queue, int queueId)
    {
        if (queueId <= 0) return null;
        var item = queue.FirstOrDefault(candidate => candidate.QueueId == queueId);
        return item is { Position: > 0 } ? item.Position : null;
    }

    internal static string[] SelectFieldNames(SpinnerConfig config)
    {
        return SongFieldNames.NormalizeSelection(
            config.WinnerDialog.Fields,
            SongFieldNames.CreateWinnerDefaultSelection());
    }
}
