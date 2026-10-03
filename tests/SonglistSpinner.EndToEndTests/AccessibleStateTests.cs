using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The states the app's toggles and fields announce to assistive technology. ARIA state attributes need the
/// strings "true" and "false"; an attribute that is empty or missing does not say the control is pressed,
/// expanded or invalid.
/// </summary>
public class AccessibleStateTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheExpandedPlayedList_When_ItIsCollapsed_Then_TheCollapseButtonReportsItNoLongerExpanded()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var collapse = scenario.Dashboard.CollapseButton;
        await Expect(collapse).ToHaveAttributeAsync("aria-expanded", "true");

        await collapse.ClickAsync();

        await Expect(collapse).ToHaveAccessibleNameAsync("Expand played songs");
        await Expect(collapse).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheSettingsPage_When_ASectionIsSelected_Then_OnlyItsNavigationButtonIsPressed()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();

        await settings.Spinner.OpenAsync();

        var navigation = scenario.Page.GetByRole(AriaRole.Navigation, new() { Name = "Settings sections" });
        await Expect(navigation.GetByRole(AriaRole.Button, new() { Name = "Spinner & Queue", Pressed = true }))
            .ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(navigation.GetByRole(AriaRole.Button, new() { Name = "Connection" }))
            .ToHaveAttributeAsync("aria-pressed", "false");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_TheDefaultPlayedFields_When_OverlayLayoutOpens_Then_SelectedFieldsArePressedAndOthersAreNot()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();

        await settings.OverlayLayout.OpenAsync();

        var playedFields = settings.OverlayLayout.PlayedFields;
        await Expect(playedFields.Toggle("Artist")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(playedFields.Toggle("Requester")).ToHaveAttributeAsync("aria-pressed", "false");
    }
}
