namespace SonglistSpinner.Simulator;

/// <summary>
/// The GitHub release the simulator's <c>/_simulator/releases/latest</c> endpoint describes, in the shape of
/// GitHub's "latest release" response, so the app's update check can be pointed at it instead of GitHub.
/// </summary>
/// <param name="Tag">The release tag, such as <c>v9.0.0</c>.</param>
/// <param name="HtmlUrl">
/// The release page. The app only offers a release whose page is under
/// <c>https://github.com/wowfood/songlistspinner-desktop/releases/</c>.
/// </param>
public sealed record SimulatedRelease(
    string Tag,
    Uri HtmlUrl,
    bool Draft = false,
    bool Prerelease = false,
    DateTimeOffset? PublishedAt = null);
