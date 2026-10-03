using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

public class ChannelSetupTests
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheEnvironmentToken_When_TheSetupWizardConnectsTheDemoChannel_Then_TheWheelHoldsItsQueue()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(cancellationToken);
        var page = scenario.App.Page;

        // Given: the token comes from the environment, so the wizard only needs the channel.
        await page.OpenSettingsAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open connection wizard" }).ClickAsync();
        await Expect(page.GetByText("A credential is already configured.")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // When
        await page.Locator("#setupReference").FillAsync("demo");
        await page.GetByRole(AriaRole.Button, new() { Name = "Connect and verify" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "SonglistSpinner is ready" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open dashboard" }).ClickAsync();

        // Then: the Dashboard loads the channel Setup saved, and its wheel (which the overlay mirrors) holds the
        // simulator's upcoming queue in order.
        await Expect(page.Locator("#streamerLabel")).ToHaveTextAsync("Streamer: demo");
        await Expect(page.AvailableCount()).ToHaveTextAsync(scenario.Channel.Queue.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        await using var overlay = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        var initialState = await overlay.NextAsync("init_state", cancellationToken);
        Assert.Equal(
            scenario.Channel.Queue.Select(entry => entry.QueueId),
            initialState.GetProperty("wheelItems").EnumerateArray().Select(item => item.GetProperty("queueId").GetInt32()));
    }
}
