using System.Security.Cryptography;
using System.Text.Json;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// A fresh temporary folder the app under test keeps all its saved state in (SONGLISTSPINNER_PROFILE_DIR). It
/// outlives an app restart and is deleted when the test ends.
/// </summary>
internal sealed class TestProfile : IAsyncDisposable
{
    // The app's test-profile file names and settings key (IsolatedProfileServiceCollectionExtensions and
    // PreferencesSettingsService); the project tests the built app, so it repeats them rather than referencing it.
    private const string PreferencesFileName = "preferences.json";
    private const string SecretsFileName = "secrets.json";
    private const string SettingsKey = "local_settings";

    private TestProfile(string directory) => Directory = directory;

    public string Directory { get; }

    private string PreferencesPath => Path.Combine(Directory, PreferencesFileName);

    public static TestProfile Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SonglistSpinner.EndToEndTests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        return new TestProfile(directory);
    }

    /// <summary>
    /// Saves <paramref name="settings"/> as the app's settings before it starts, as a user who saved them earlier
    /// would have. Pass an anonymous object with <c>SettingsDto</c>'s JSON names, for example
    /// <c>new { DefaultStreamerName = "streamer", PlayedListShowNumbers = true }</c>; properties left out keep
    /// their defaults. Two differ from the C# names: <c>PlayedListFields</c> is saved as <c>SongListFields</c> and
    /// <c>UpdateQueueAfterSpin</c> as <c>AutoPlay</c>. An unknown name is silently ignored. The app reads the file
    /// only at startup.
    /// </summary>
    public void SaveSettings(object settings)
    {
        var values = new Dictionary<string, string> { [SettingsKey] = JsonSerializer.Serialize(settings) };
        File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(values));
    }

    /// <summary>
    /// A fingerprint of everything the app saves to the profile (settings, the dismissed update and the API
    /// credential). The app rewrites the files on every save, so a different fingerprint means it saved something.
    /// </summary>
    public string ReadSavedStateFingerprint()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var name in new[] { PreferencesFileName, SecretsFileName })
        {
            var path = Path.Combine(Directory, name);
            hash.AppendData(File.Exists(path) ? File.ReadAllBytes(path) : [0]);
            hash.AppendData([0xFF]);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public async ValueTask DisposeAsync()
    {
        // WebView2's browser processes let go of their files a moment after the app is killed, so deleting retries
        // for a few seconds. This is cleanup, not test synchronisation: a folder that stays locked is left behind.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }
    }
}
