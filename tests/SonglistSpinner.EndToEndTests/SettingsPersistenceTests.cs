using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class SettingsPersistenceTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_ASavedSetting_When_TheAppRestartsOnTheSameProfile_Then_TheSettingIsKept()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        await scenario.App.Page.OpenSettingsAsync();
        await scenario.App.Page.EnableNowPlayingWorkflowAsync();

        await scenario.RestartAppAsync(cancellationToken);

        var page = scenario.App.Page;
        await page.OpenSettingsAsync();
        await page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Spinner & Queue" }).ClickAsync();
        await Expect(page.NowPlayingWorkflowCheckbox()).ToBeCheckedAsync();
    }
}
