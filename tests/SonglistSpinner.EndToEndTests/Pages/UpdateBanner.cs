using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The banner offering a newer release, checked once per app run against <c>SimulatedRelease</c>: "SonglistSpinner
/// 9.9.9 is available", "You are currently using 1.2.0.", a "View release" link and a dismiss button.
/// </summary>
internal sealed partial class UpdateBanner(IPage page)
{
    public ILocator Root => page.Locator(".ss-update-banner");

    public ILocator Headline => Root.Locator(".ss-update-banner__message strong");

    public ILocator CurrentVersion => Root.Locator(".ss-update-banner__message span");

    public ILocator ViewReleaseLink => Root.GetByRole(AriaRole.Link, new() { Name = "View release" });

    public ILocator DismissButton =>
        Root.GetByRole(AriaRole.Button, new() { Name = "Dismiss this update notification" });

    public async Task DismissAsync()
    {
        await DismissButton.ClickAsync();
        await Expect(Root).ToBeHiddenAsync();
    }
}
