namespace SonglistSpinner.Simulator;

/// <summary>
/// The channel <c>--seed demo</c> creates for manual testing: <c>demo</c> on Twitch, streamer 1001, with a Now
/// Playing song, six upcoming requests (two with tips, one already played today) and play history spread over
/// the last two months, so each play-history period setting gives a different exclusion result.
/// </summary>
internal static class DemoSeed
{
    public const string ChannelName = "demo";
    public const int StreamerId = 1001;

    public static async Task ApplyAsync(StreamerSongListSimulator simulator, TimeProvider time)
    {
        var channel = simulator.AddChannel(ChannelName, "twitch", StreamerId);
        var now = time.GetUtcNow();

        await channel.AddPlayedSongAsync("Daft Punk", "Get Lucky", now.AddHours(-1), "early_bird");
        await channel.AddPlayedSongAsync("Queen", "Don't Stop Me Now", now.AddDays(-3), "regular_viewer");
        await channel.AddPlayedSongAsync("ABBA", "Dancing Queen", now.AddDays(-20), "disco_fan", 5.00m);
        await channel.AddPlayedSongAsync("Toto", "Africa", now.AddDays(-60), "long_time_fan");

        var playing = await channel.RequestSongAsync("Fleetwood Mac", "Dreams", "night_owl");
        await channel.SetNowPlayingAsync(playing.QueueId);

        await channel.RequestSongAsync("Daft Punk", "Get Lucky", "early_bird");
        await channel.RequestSongAsync("a-ha", "Take On Me", "synth_lover", 3.50m);
        await channel.RequestSongAsync("Queen", "Don't Stop Me Now", "regular_viewer");
        await channel.RequestSongAsync("The Killers", "Mr. Brightside", "indie_kid");
        await channel.RequestSongAsync("Toto", "Africa", "long_time_fan", 10.00m);
        await channel.RequestSongAsync("Journey", "Don't Stop Believin'", "karaoke_star");
    }
}
