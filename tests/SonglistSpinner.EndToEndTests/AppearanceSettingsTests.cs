using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// Settings &gt; Appearance's readability check, which warns when overlay text and its panel, or button text and its
/// button, contrast less than 4.5:1. Its colour pickers have no stable handles, so the colours are saved before the
/// app starts.
/// </summary>
public class AppearanceSettingsTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_GreyTextOnTheDefaultPlayedCardsSaved_When_AppearanceIsOpened_Then_TheReadabilityCheckNamesOnlyThePlayedCards()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        // #777777 on the default #222222 cards is about 3.7:1; the default #CCCCCC buttons on #555555 are about 4.6:1.
        await using var scenario = await AppScenario.StartAsync(cancellationToken, prepare: (_, profile) =>
        {
            profile.SaveSettings(new { ColorText = "#777777" });
            return Task.CompletedTask;
        });
        var settings = scenario.Settings;
        await settings.OpenAsync();

        await settings.Appearance.OpenAsync();

        await Expect(settings.Appearance.ContrastWarning).ToHaveTextAsync(
            "Readability check Increase the contrast for overlay text on played-song cards. " +
            "Aim for at least 4.5:1 for normal text.");
    }
}
