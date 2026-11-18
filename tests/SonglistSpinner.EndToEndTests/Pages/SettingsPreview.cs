using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The Settings page's live overlay preview: the overlay page at <c>?preview=1</c> in a frame, rendering the draft
/// settings over fixed sample songs for the default channel ("your-channel" when none is set).
/// </summary>
internal sealed class SettingsPreview(IFrameLocator frame)
{
    public ILocator StreamerLabel => frame.Locator("#streamerLabel");

    public PlayedListPanel PlayedList => new(frame);
}
