using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>Settings &gt; Spinner &amp; Queue: winner actions, played-song exclusion and the history period.</summary>
internal sealed partial class SpinnerSettings(IPage page) : SettingsSection(page, "Spinner & Queue")
{
    public ILocator PreferMarkPlayed => Checkbox("Prefer Mark Played in the winner popup");

    public ILocator NowPlayingWorkflow => Checkbox("Enable Now Playing workflow");

    public ILocator ExcludePlayedSongs => Checkbox("Exclude already-played songs from the wheel");

    /// <summary>
    /// Values: <c>stream</c> ("Most recent plays"), <c>day</c> ("Last 24 hours"), <c>week</c> ("Last 7 days",
    /// the default), <c>month</c> ("Last month"), <c>all</c> ("All time").
    /// </summary>
    public ILocator PlayHistoryPeriod => Page.Locator("#playHistoryPeriod");

    public Task SelectPlayHistoryPeriodAsync(string value) => PlayHistoryPeriod.SelectOptionAsync(value);
}
