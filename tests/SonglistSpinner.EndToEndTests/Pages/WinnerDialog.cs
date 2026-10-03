using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The Dashboard's winner dialog: the winner's fields as label and value pairs, its queue position, and the
/// actions. Each action waits for the dialog to close; a failed action leaves it open with <see cref="Error"/>.
/// </summary>
internal sealed partial class WinnerDialog(IPage page)
{
    public ILocator Root => page.Locator("#winnerModal");

    /// <summary>The dialog's card, which takes the Winner Dialog Settings' width, font and font size.</summary>
    public ILocator Card => Root.Locator(".winner-modal-content");

    public ILocator Labels => Root.Locator(".winner-field-label");

    public ILocator Values => Root.Locator(".winner-field-value");

    /// <summary>The position's text, such as "#2"; absent when the dialog shows none.</summary>
    public ILocator QueuePosition => Root.Locator(".winner-queue-position strong");

    public ILocator SetNowPlayingButton => page.Locator("#setWinnerNowPlayingBtn");

    public ILocator MarkPlayedButton => page.Locator("#markWinnerPlayedBtn");

    public ILocator LeaveInQueueButton => page.Locator("#leaveWinnerInQueueBtn");

    /// <summary>"StreamerSongList failed while ...: reason" after a failed action.</summary>
    public ILocator Error => Root.Locator(".winner-action-error");

    /// <summary>Waits until the dialog shows exactly these fields, in order.</summary>
    public async Task ExpectFieldsAsync(params (string Label, string Value)[] fields)
    {
        await Expect(Labels).ToHaveTextAsync(fields.Select(field => field.Label).ToArray());
        await Expect(Values).ToHaveTextAsync(fields.Select(field => field.Value).ToArray());
    }

    /// <summary>The values the open dialog shows, in order.</summary>
    public async Task<IReadOnlyList<string>> ReadValuesAsync()
    {
        await Expect(Values.First).ToBeVisibleAsync();
        return [.. (await Values.AllTextContentsAsync()).Select(value => value.Trim())];
    }

    public Task MarkPlayedAsync() => ChooseAsync(MarkPlayedButton);

    public Task SetNowPlayingAsync() => ChooseAsync(SetNowPlayingButton);

    public Task LeaveInQueueAsync() => ChooseAsync(LeaveInQueueButton);

    /// <summary>Dismisses the dialog with Escape, which leaves the winner in the queue.</summary>
    public async Task EscapeAsync()
    {
        await page.Keyboard.PressAsync("Escape");
        await Expect(Root).ToBeHiddenAsync();
    }

    private async Task ChooseAsync(ILocator action)
    {
        await action.ClickAsync();
        await Expect(Root).ToBeHiddenAsync();
    }
}
