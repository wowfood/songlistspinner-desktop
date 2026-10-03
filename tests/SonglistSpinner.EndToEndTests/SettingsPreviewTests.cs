using SonglistSpinner.EndToEndTests.Infrastructure;
using SonglistSpinner.EndToEndTests.Scenarios;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>
/// The Settings page's live overlay preview: the overlay page at <c>?preview=1</c> in a frame, fed the draft settings
/// and fixed sample songs (SettingsPreview) instead of the app's event stream.
/// </summary>
public class SettingsPreviewTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheSettingsPreview_When_SequenceNumbersAreCheckedWithoutSaving_Then_OnlyThePreviewNumbersItsSampleSongs()
    {
        EndToEnd.SkipUnlessEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await sharedApp.BeginTestAsync(cancellationToken);
        await new ChannelSeed("preview_streamer")
            .WithPlayed(SongCatalog.GetLucky, TimeSpan.FromHours(1))
            .WithQueued(SongCatalog.TakeOnMe)
            .ApplyAsync(scenario.Simulator);
        var dashboard = scenario.Dashboard;
        await dashboard.LoadChannelAsync("preview_streamer");
        await using var obsSource = await OverlayEventStream.ConnectAsync(scenario.App.OverlayEventsUri, cancellationToken);
        await obsSource.NextAsync("init_state", cancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.OverlayLayout.OpenAsync();
        var preview = settings.Preview;
        // The preview shows SettingsPreview's first three samples as played, newest first, for the default channel.
        await Expect(preview.StreamerLabel).ToHaveTextAsync("your-channel");
        await preview.PlayedList.ExpectLinesAsync(
            "Artist: The Midnight | Title: Sunset",
            "Artist: CHVRCHES | Title: Clearest Blue",
            "Artist: Daft Punk | Title: Digital Love");

        await settings.OverlayLayout.PlayedShowNumbers.CheckAsync();

        await preview.PlayedList.ExpectLinesAsync(
            "3. Artist: The Midnight | Title: Sunset",
            "2. Artist: CHVRCHES | Title: Clearest Blue",
            "1. Artist: Daft Punk | Title: Digital Love");
        await Expect(settings.DraftState).ToHaveTextAsync("Unsaved draft");
        // Returning to the Dashboard sends the live overlay its (saved) settings. Had the preview reached the live
        // overlay, its update would have come first, so the first update a browser source sees is the proof.
        await settings.ClickDashboardLinkAsync();
        await settings.UnsavedChangesPrompt.ChooseAsync("Abandon changes");
        var firstUpdate = await obsSource.NextAsync("update_songs", cancellationToken);
        Assert.Equal("preview_streamer", firstUpdate.GetProperty("streamer").GetString());
        Assert.False(firstUpdate.GetProperty("config").GetProperty("playedList").GetProperty("showNumbers").GetBoolean());
        Assert.Equal(
            ["Artist: Daft Punk | Title: Get Lucky"],
            firstUpdate.GetProperty("playedTexts").EnumerateArray().Select(text => text.GetString()));
    }
}
