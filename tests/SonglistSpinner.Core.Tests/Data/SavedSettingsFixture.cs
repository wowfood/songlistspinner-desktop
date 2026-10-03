using System.Text.Json;
using SonglistSpinner.Core.Data;

namespace SonglistSpinner.Core.Tests.Data;

/// <summary>
/// Settings JSON captured from a released version, read the way <c>PreferencesSettingsService</c> loads it.
/// The fixtures are the persisted contract: never regenerate them from the current <see cref="SettingsDto"/>.
/// </summary>
internal static class SavedSettingsFixture
{
    public const string Release120Defaults = "settings-1.2.0-defaults.json";
    public const string Release120Customised = "settings-1.2.0-customised.json";

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
        SettingsDtoNormalizer.Normalize(
            JsonSerializer.Deserialize<SettingsDto>(ReadJson(fixtureName), PreferencesJsonOptions)!);

    public static string Save(SettingsDto settings) =>
        JsonSerializer.Serialize(SettingsDtoNormalizer.Normalize(settings), PreferencesJsonOptions);
}
