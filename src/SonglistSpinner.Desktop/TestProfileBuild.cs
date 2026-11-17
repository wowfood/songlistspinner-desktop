namespace SonglistSpinner;

/// <summary>
/// Whether this build of the app honours the test-profile variables (<c>SONGLISTSPINNER_PROFILE_DIR</c> and
/// <c>SONGLISTSPINNER_WEBVIEW_ARGS</c>). They keep the API credential in a plain-text file and can open a
/// browser debugging port, so only builds made for development and testing compile them in.
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
