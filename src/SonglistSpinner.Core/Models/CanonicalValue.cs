namespace SonglistSpinner.Core.Models;

internal static class CanonicalValue
{
    // Matches a stored or user-supplied value against a catalog of canonical values, ignoring case and
    // surrounding whitespace, and returns the catalog's spelling.
    public static bool TryNormalize(string? value, IEnumerable<string> canonicalValues, out string normalized)
    {
        var candidate = value?.Trim();
        foreach (var canonical in canonicalValues)
        {
            if (!string.Equals(candidate, canonical, StringComparison.OrdinalIgnoreCase)) continue;
            normalized = canonical;
            return true;
        }

        normalized = "";
        return false;
    }
}
