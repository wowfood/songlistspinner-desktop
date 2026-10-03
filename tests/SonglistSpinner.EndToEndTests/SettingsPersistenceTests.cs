using SonglistSpinner.EndToEndTests.Infrastructure;
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
        // A restart is the behaviour under test, so this test has its own app rather than the class's shared one.
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await settings.Spinner.NowPlayingWorkflow.CheckAsync();
        await settings.SaveAsync();

        await scenario.RestartAppAsync(cancellationToken);

        settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Spinner.OpenAsync();
        await Expect(settings.Spinner.NowPlayingWorkflow).ToBeCheckedAsync();
    }
}
