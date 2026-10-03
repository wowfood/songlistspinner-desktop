using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings draft's CSS values that failed validation when the user last saved, keyed by
/// <see cref="SettingsDto"/> property name, with the message shown under each field. A field stays marked invalid
/// until the user edits it.
/// </summary>
public sealed class CssValidationErrors
{
    private readonly Dictionary<string, string> _errors;

    public CssValidationErrors(IDictionary<string, string>? errors = null)
    {
        _errors = errors is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(errors, StringComparer.Ordinal);
    }

    public bool IsEmpty => _errors.Count == 0;

    public bool Has(string key) => _errors.ContainsKey(key);

    public string? ErrorFor(string key) => _errors.GetValueOrDefault(key);

    /// <summary>The CSS class that marks the field invalid, or none.</summary>
    public string? FieldClass(string key) => Has(key) ? "invalid" : null;

    /// <returns><see langword="true"/> when the field had an error.</returns>
    public bool Remove(string key) => _errors.Remove(key);
}
