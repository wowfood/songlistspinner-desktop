using System.Text.Json;
using System.Text.Json.Nodes;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class SpinnerConfigTests
{
    private static readonly JsonSerializerOptions OverlayJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    [Fact]
    public void Given_DefaultConfig_When_ItsWheelColorIsChanged_Then_NewConfigsKeepTheDefaultColors()
    {
        var changed = new SpinnerConfig();

        changed.WheelColors[0] = "#000000";

        Assert.Equal(
            ["#ff6b6b", "#4ecdc4", "#45b7d1", "#f9ca24", "#6c5ce7", "#a29bfe", "#fd79a8", "#fdcb6e"],
            new SpinnerConfig().WheelColors);
    }

    [Fact]
    public void Given_DefaultConfig_When_SerializedForTheOverlay_Then_MatchesThePreviousDefaults()
    {
        // The wheel and the OBS overlay receive this shape; the values must not change by accident.
        const string expected = """
            {
              "debug": false,
              "wheelColors": ["#ff6b6b", "#4ecdc4", "#45b7d1", "#f9ca24", "#6c5ce7", "#a29bfe", "#fd79a8", "#fdcb6e"],
              "background": { "mode": "color", "color": "#111111", "image": "" },
              "streamer": { "defaultName": "", "platform": "twitch", "hideChangeOptionWhenDefault": true },
              "songList": {
                "fields": ["artist", "title"],
                "excludePlayedSongs": false,
                "playedListPosition": "right",
                "playHistoryPeriod": "week"
              },
              "playedList": {
                "fontFamily": "sans-serif",
                "fontSize": "0.875rem",
                "maxLines": 2,
                "showNumbers": false,
                "numberingStart": "bottom",
                "separator": " | ",
                "showLabels": true,
                "showFieldHeaders": false
              },
              "nowPlaying": {
                "enabled": false,
                "fields": ["artist", "title"],
                "separator": " | ",
                "showLabels": true,
                "fontFamily": "sans-serif",
                "fontSize": "1.125rem",
                "width": "28rem",
                "position": "bottom-left"
              },
              "winnerDialog": {
                "fields": ["artist", "title", "requester"],
                "fontFamily": "sans-serif",
                "fontSize": "1rem",
                "width": "36rem",
                "showQueuePosition": true
              },
              "colors": {
                "text": "#ffffff",
                "statusBackground": "rgba(0, 0, 0, 0.7)",
                "playedListBackground": "rgba(0, 0, 0, 0.7)",
                "nowPlayingBackground": "rgba(0, 0, 0, 0.7)",
                "playedItemBackground": "#222222",
                "resizeHandleBackground": "#333333",
                "resizeHandleHoverBackground": "#555555",
                "toggleBackground": "#222222",
                "buttonBackground": "#555555",
                "buttonText": "#CCCCCC",
                "pointer": "wheat"
              }
            }
            """;

        var json = JsonSerializer.Serialize(new SpinnerConfig(), OverlayJsonOptions);

        Assert.Equal(ToComparableJson(expected), ToComparableJson(json));
    }

    private static string ToComparableJson(string json) =>
        JsonNode.Parse(json)!.ToJsonString(IndentedJsonOptions);
}
