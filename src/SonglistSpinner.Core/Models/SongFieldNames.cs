namespace SonglistSpinner.Core.Models;

public static class SongFieldNames
{
    public const string Artist = "artist";
    public const string Title = "title";
    public const string Requester = "requester";
    public const string Donation = "donation";

    public const string DefaultJson = """["artist","title"]""";
    public const string WinnerDefaultJson = """["artist","title","requester"]""";

    private static readonly string[] SupportedValues = [Artist, Title, Requester, Donation];

    public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

    public static string[] CreateDefaultSelection() => [Artist, Title];

    public static string[] CreateWinnerDefaultSelection() => [Artist, Title, Requester];

    public static bool TryNormalize(string? value, out string normalized)
    {
        var candidate = value?.Trim();
        foreach (var supported in SupportedValues)
        {
            if (!string.Equals(candidate, supported, StringComparison.OrdinalIgnoreCase)) continue;
            normalized = supported;
            return true;
        }

        normalized = "";
        return false;
    }

    public static string[] NormalizeSelection(
        IEnumerable<string>? values,
        IEnumerable<string>? fallback = null)
    {
        var normalized = NormalizeKnownValues(values);
        if (normalized.Length > 0) return normalized;

        normalized = NormalizeKnownValues(fallback);
        return normalized.Length > 0 ? normalized : CreateDefaultSelection();
    }

    private static string[] NormalizeKnownValues(IEnumerable<string>? values)
    {
        if (values is null) return [];

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!TryNormalize(value, out var normalized) || !seen.Add(normalized)) continue;
            result.Add(normalized);
        }

        return result.ToArray();
    }
}
