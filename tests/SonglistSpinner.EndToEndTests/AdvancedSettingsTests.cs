using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>Settings &gt; Advanced: the endpoints the running app uses, for troubleshooting.</summary>
public class AdvancedSettingsTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheAppOnTheSimulator_When_AdvancedIsOpened_Then_ItShowsTheVersionApiAndOverlayInUse()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();

        await settings.Advanced.OpenAsync();

        await Expect(settings.Advanced.Endpoint("Application")).ToHaveTextAsync($"SonglistSpinner {AppVersion.Current}");
        await Expect(settings.Advanced.Endpoint("API")).ToHaveTextAsync(scenario.Simulator.ApiBaseAddress.ToString());
        await Expect(settings.Advanced.Endpoint("Local overlay"))
            .ToHaveTextAsync($"http://localhost:{scenario.App.OverlayPort}/overlay");
    }
}
