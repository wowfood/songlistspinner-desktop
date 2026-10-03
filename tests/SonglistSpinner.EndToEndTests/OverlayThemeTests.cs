using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Pages;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>The theme colours and page background an OBS browser source gets from the saved Appearance settings.</summary>
public class OverlayThemeTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_DefaultAppearance_When_TheOverlayShowsTheChannel_Then_ItUsesWhiteTextOnASolidDarkBackground()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);

        var overlay = await OpenOverlayOnLoadedChannelAsync(scenario);

        Assert.Equal("#ffffff", await overlay.ReadThemeVariableAsync("--app-text-color"));
        Assert.Equal("rgba(0, 0, 0, 0.7)", await overlay.ReadThemeVariableAsync("--app-played-list-bg"));
        Assert.Equal("rgb(17, 17, 17)", await overlay.ReadBackgroundColorAsync());
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ATransparentBackgroundSaved_When_TheOverlayShowsTheChannel_Then_ItsPageIsTransparent()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.BackgroundMode.SelectOptionAsync("transparent");
        await settings.SaveAsync();
        await scenario.Dashboard.OpenAsync();

        var overlay = await OpenOverlayOnLoadedChannelAsync(scenario);

        Assert.Equal("rgba(0, 0, 0, 0)", await overlay.ReadBackgroundColorAsync());
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_PlayedPanelOpacityFiftySaved_When_TheOverlayShowsTheChannel_Then_ThePanelIsHalfTransparentBlack()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.PlayedPanelOpacity.FillAsync("50");
        await settings.SaveAsync();
        await scenario.Dashboard.OpenAsync();

        var overlay = await OpenOverlayOnLoadedChannelAsync(scenario);

        Assert.Equal("rgba(0,0,0,0.50)", await overlay.ReadThemeVariableAsync("--app-played-list-bg"));
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ASeparateNowPlayingOpacityOfFortySaved_When_TheOverlayShowsTheChannel_Then_OnlyTheNowPlayingPanelIsFortyPercentBlack()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Appearance.OpenAsync();
        await settings.Appearance.UseSeparateNowPlayingOpacity.CheckAsync();
        await settings.Appearance.NowPlayingOpacity.FillAsync("40");
        await settings.SaveAsync();
        await scenario.Dashboard.OpenAsync();

        var overlay = await OpenOverlayOnLoadedChannelAsync(scenario);

        Assert.Equal("rgba(0,0,0,0.40)", await overlay.ReadThemeVariableAsync("--app-now-playing-bg"));
        // Saving writes the played panel's unchanged 70% in the same rgba form as the opacity it now stores.
        Assert.Equal("rgba(0,0,0,0.70)", await overlay.ReadThemeVariableAsync("--app-played-list-bg"));
    }

    /// <summary>
    /// Loads a channel and opens the overlay on it. The overlay applies the theme and background in the same state
    /// update that names the channel, so once the label shows the channel the theme can be read once.
    /// </summary>
    private static async Task<OverlayPage> OpenOverlayOnLoadedChannelAsync(AppScenario scenario)
    {
        await new ChannelSeed("theme_streamer").WithQueued(SongCatalog.TakeOnMe).ApplyAsync(scenario.Simulator);
        await scenario.Dashboard.LoadChannelAsync("theme_streamer");
        var overlay = await scenario.OpenOverlayAsync();
        await Expect(overlay.StreamerLabel).ToHaveTextAsync("theme_streamer");
        return overlay;
    }
}
