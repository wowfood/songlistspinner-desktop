using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.Simulator;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The banner offering a newer release. The app checks once per run, at startup, so each test sets the release
/// before its own app starts.
/// </summary>
/// <remarks>
/// An absent banner has no event of its own, so those tests wait until the simulator has answered the app's check
/// and then for one navigation through the app, which renders the layout the banner lives in, before asserting.
/// </remarks>
public class UpdateBannerTests
{
    private const string ReleasePage = "https://github.com/wowfood/songlistspinner-desktop/releases/tag/v9.9.9";

    [Fact(Timeout = 180_000)]
    public async Task Given_ANewerRelease_When_TheAppStarts_Then_TheBannerOffersItWithTheCurrentVersionAndItsReleasePage()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(
            cancellationToken,
            prepare: (simulator, _) => SetRelease(simulator, new SimulatedRelease("v9.9.9", new Uri(ReleasePage))));

        var banner = scenario.UpdateBanner;
        await Expect(banner.Headline).ToHaveTextAsync("SonglistSpinner 9.9.9 is available");
        await Expect(banner.CurrentVersion).ToHaveTextAsync($"You are currently using {AppVersion.Current}.");
        await Expect(banner.ViewReleaseLink).ToHaveAttributeAsync("href", ReleasePage);
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_ADismissedRelease_When_TheAppRestarts_Then_TheBannerStaysHidden()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await AppScenario.StartAsync(
            cancellationToken,
            prepare: (simulator, _) => SetRelease(simulator, new SimulatedRelease("v9.9.9", new Uri(ReleasePage))));
        await Expect(scenario.UpdateBanner.Headline).ToHaveTextAsync("SonglistSpinner 9.9.9 is available");
        await scenario.UpdateBanner.DismissAsync();
        var firstCheck = await scenario.Simulator.WaitForFirstLatestReleaseRequestAsync(_ => true, cancellationToken);

        await scenario.RestartAppAsync(cancellationToken);

        // The restarted app checks again and is offered the same release, which it must not show.
        var secondCheck = await scenario.Simulator.WaitForFirstLatestReleaseRequestAsync(
            check => !ReferenceEquals(check, firstCheck),
            cancellationToken);
        Assert.Equal(200, secondCheck.StatusCode);
        await ExpectNoBannerAsync(scenario);
    }

    /// <summary>
    /// Releases the app must not offer: the version it is running, a prerelease, and a release published from
    /// another repository.
    /// </summary>
    public static TheoryData<string, string, bool> ReleasesNotOffered =>
        new()
        {
            { $"v{AppVersion.Current}", ReleasePage, false },
            { "v9.9.9", ReleasePage, true },
            { "v9.9.9", "https://github.com/someone-else/songlistspinner-desktop/releases/tag/v9.9.9", false }
        };

    [Theory(Timeout = 180_000)]
    [MemberData(nameof(ReleasesNotOffered))]
    public async Task Given_AReleaseTheAppMustNotOffer_When_TheAppStarts_Then_NoBannerIsShown(
        string tag,
        string releasePage,
        bool prerelease)
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scenario = await AppScenario.StartAsync(
            cancellationToken,
            prepare: (simulator, _) =>
                SetRelease(simulator, new SimulatedRelease(tag, new Uri(releasePage), Prerelease: prerelease)));

        var check = await scenario.Simulator.WaitForFirstLatestReleaseRequestAsync(_ => true, cancellationToken);
        Assert.Equal(200, check.StatusCode);
        await ExpectNoBannerAsync(scenario);
    }

    private static Task SetRelease(StreamerSongListSimulator simulator, SimulatedRelease release)
    {
        simulator.LatestRelease = release;
        return Task.CompletedTask;
    }

    private static async Task ExpectNoBannerAsync(AppScenario scenario)
    {
        await scenario.Settings.OpenAsync();
        await scenario.Dashboard.OpenAsync();
        await Expect(scenario.UpdateBanner.Root).ToHaveCountAsync(0);
    }
}
