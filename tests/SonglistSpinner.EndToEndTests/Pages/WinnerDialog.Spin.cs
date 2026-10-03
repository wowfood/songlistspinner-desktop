using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class WinnerDialog
{
    // Exactly one action, the one the settings suggest, is primary; the others are secondary.
    private static readonly Regex PrimaryAction = new(@"(^|\s)winner-action-primary(\s|$)");

    private static readonly Regex SecondaryAction = new(@"(^|\s)winner-action-secondary(\s|$)");

    /// <summary>Presses Mark Played without waiting for the dialog to close, for an action expected to fail.</summary>
    public Task ClickMarkPlayedAsync() => MarkPlayedButton.ClickAsync();

    /// <summary>Presses Set Now Playing without waiting for the dialog to close, for an action expected to fail.</summary>
    public Task ClickSetNowPlayingAsync() => SetNowPlayingButton.ClickAsync();

    /// <summary>Waits until the open dialog styles <paramref name="action"/> as the suggested one and focuses it.</summary>
    public async Task ExpectPrimaryActionAsync(ILocator action)
    {
        await Expect(Root).ToBeVisibleAsync();
        await Expect(action).ToHaveClassAsync(PrimaryAction);
        await Expect(action).ToBeFocusedAsync();
    }

    /// <summary>Waits until the open dialog styles <paramref name="action"/> as a secondary one.</summary>
    public async Task ExpectSecondaryActionAsync(ILocator action)
    {
        await Expect(Root).ToBeVisibleAsync();
        await Expect(action).ToHaveClassAsync(SecondaryAction);
    }
}
