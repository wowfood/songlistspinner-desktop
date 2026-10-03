namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// A fresh temporary folder the app under test keeps all its saved state in (SONGLISTSPINNER_PROFILE_DIR). It
/// outlives an app restart and is deleted when the test ends.
/// </summary>
internal sealed class TestProfile : IAsyncDisposable
{
    private TestProfile(string directory) => Directory = directory;

    public string Directory { get; }

    public static TestProfile Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SonglistSpinner.EndToEndTests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        return new TestProfile(directory);
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
