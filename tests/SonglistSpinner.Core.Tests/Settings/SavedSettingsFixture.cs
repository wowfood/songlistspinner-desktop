using System.Text.Json;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Core.Tests.Settings;

/// <summary>
/// Saved settings JSON, read the way <c>PreferencesSettingsService</c> loads it. The fixtures are the persisted
/// contract: never regenerate them from the current <see cref="SettingsDto"/>.
/// </summary>
internal static class SavedSettingsFixture
{
    /// <summary>
    /// Customised settings as release 1.2.0 saved them: exactly the 32 properties its SettingsDto had, compact,
    /// with no winner-dialog, separator, label, header, numbering or Now Playing opacity keys.
    /// </summary>
    public const string Release120Customised = "settings-1.2.0-customised.json";

    // Default and customised settings as develop saved them at 4f36e18, before the coding-standards renames.
    // They hold every property that version had, so loading and saving them again must not change a byte.
    public const string Develop4f36e18Defaults = "settings-develop-4f36e18-defaults.json";
    public const string Develop4f36e18Customised = "settings-develop-4f36e18-customised.json";

    // Matches PreferencesSettingsService, which reads and writes the Preferences value.
    private static readonly JsonSerializerOptions PreferencesJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string ReadJson(string fixtureName)
    {
        using var stream = typeof(SavedSettingsFixture).Assembly.GetManifestResourceStream($"Fixtures.{fixtureName}")
                           ?? throw new InvalidOperationException($"Missing embedded fixture '{fixtureName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static SettingsDto Load(string fixtureName) =>
        SettingsDtoNormalizer.NormalizeInPlace(
            JsonSerializer.Deserialize<SettingsDto>(ReadJson(fixtureName), PreferencesJsonOptions)!);

    public static string Save(SettingsDto settings) =>
        JsonSerializer.Serialize(SettingsDtoNormalizer.NormalizeInPlace(settings), PreferencesJsonOptions);
}
