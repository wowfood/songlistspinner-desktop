using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.Simulator;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class DashboardPage
{
    /// <summary>The Now Playing panel: the song and its Mark Played button, shown while a song is playing.</summary>
    public ILocator NowPlayingControl => Page.Locator("#nowPlayingControl");

    /// <summary>
    /// Loads <paramref name="name"/>, then waits until the refresh that realtime updates make once connected (a
    /// second queue-and-history read, about 300 ms after the load) has been answered. Until then that refresh can
    /// land in the middle of a test's spin, adding calls to the request log or taking a fault queued for the spin.
    /// </summary>
    public async Task LoadChannelAndSettleAsync(
        string name,
        StreamerSongListSimulator simulator,
        CancellationToken cancellationToken)
    {
        await LoadChannelAsync(name);
        await Expect(Health.Realtime).ToHaveTextAsync("Connected");
        await simulator.WaitForFirstRequestAsync(Nth(ApiCalls.FetchQueue(name), 2), cancellationToken);
        await simulator.WaitForFirstRequestAsync(Nth(ApiCalls.FetchPlayHistory(name), 2), cancellationToken);
    }

    /// <summary>
    /// Presses the played panel's SPIN, shown while the wheel is hidden, and waits for the winner dialog.
    /// </summary>
    public async Task<WinnerDialog> SpinFromPlayedListAsync()
    {
        await PlayedListSpinButton.ClickAsync();
        await Expect(Winner.Root).ToBeVisibleAsync();
        return Winner;
    }

    // The request log offers each request to a waiter once, oldest first, so counting matches finds the Nth.
    private static Func<RecordedRequest, bool> Nth(Func<RecordedRequest, bool> match, int occurrence)
    {
        var seen = 0;
        return request => match(request) && ++seen == occurrence;
    }
}
