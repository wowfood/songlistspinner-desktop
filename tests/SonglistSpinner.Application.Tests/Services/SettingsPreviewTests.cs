using SonglistSpinner.Core.Models;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class SettingsPreviewTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_NoDefaultChannel_When_CreatingThePreview_Then_ItShowsThePlaceholderChannel(string defaultStreamerName)
    {
        var payload = SettingsPreview.CreatePayload(new SpinnerConfig(), defaultStreamerName);

        Assert.Equal("your-channel", payload.Streamer);
    }

    [Fact]
    public void Given_DefaultChannel_When_CreatingThePreview_Then_ItShowsTheTrimmedChannel()
    {
        var payload = SettingsPreview.CreatePayload(new SpinnerConfig(), "  wowfood ");

        Assert.Equal("wowfood", payload.Streamer);
    }

    [Fact]
    public void Given_DraftSettings_When_CreatingThePreview_Then_SamplesFillTheWheelPlayedListAndNowPlaying()
    {
        var config = new SpinnerConfig();

        var payload = SettingsPreview.CreatePayload(config, "wowfood");

        Assert.Same(config, payload.Config);
        Assert.Equal(
            [
                "The Midnight - Sunset (mod_jane)", "CHVRCHES - Clearest Blue (musicfan)",
                "Daft Punk - Digital Love (alex)", "Florence + The Machine - Dog Days Are Over (streamviewer)"
            ],
            payload.WheelItems.Select(item => item.Label));
        Assert.All(payload.WheelItems, item => Assert.Null(item.QueueId));
        Assert.Equal(3, payload.PlayedTexts.Length);
        Assert.Equal(3, payload.PlayedCount);
        Assert.Equal(4, payload.AvailableCount);
        Assert.Equal("Artist: Florence + The Machine | Title: Dog Days Are Over", payload.NowPlayingText);
    }
}
