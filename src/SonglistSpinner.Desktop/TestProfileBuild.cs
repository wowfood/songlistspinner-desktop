namespace SonglistSpinner;

/// <summary>
/// Whether this build of the app honours the development variables: the test profile
/// (<c>SONGLISTSPINNER_PROFILE_DIR</c>, <c>SONGLISTSPINNER_WEBVIEW_ARGS</c>), the StreamerSongList address and
/// token overrides (<c>SONGLISTSPINNER_SSL_*</c>) and <c>SONGLISTSPINNER_UPDATE_RELEASE_URL</c>. Between them
/// they can keep the API credential in a plain-text file, open a browser debugging port and send the saved token
/// to another server, so only builds made for development and testing compile them in.
/// </summary>
/// <remarks>
/// The <c>EnableTestProfile</c> MSBuild property decides: true for Debug builds and for builds that pass
/// <c>-p:EnableTestProfile=true</c> (scripts/run-e2e.ps1 does), false for a Release build and so for every
/// published executable. The same property stamps the assembly, which is how the end-to-end tests recognise a
/// build they may start.
/// </remarks>
internal static class TestProfileBuild
{
#if TEST_PROFILE
    public static bool IsEnabled => true;
#else
    public static bool IsEnabled => false;
#endif
}
