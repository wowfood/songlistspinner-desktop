using System.Globalization;
using SonglistSpinner.Simulator;

// dotnet run --project tests/SonglistSpinner.StreamerSongListSimulator -- [--port 5199] [--seed demo|none] [--token <token>]
const string Usage = "Usage: [--port <port>] [--seed demo|none] [--token <token>]";
var port = 5199;
var seed = "demo";
var token = StreamerSongListSimulatorOptions.DefaultAccessToken;
for (var i = 0; i < args.Length; i += 2)
{
    var value = i + 1 < args.Length ? args[i + 1] : "";
    var valid = args[i] switch
    {
        "--port" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port),
        "--seed" => value is "demo" or "none",
        "--token" => !string.IsNullOrWhiteSpace(value),
        _ => false
    };
    if (!valid)
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }

    if (args[i] == "--seed") seed = value;
    if (args[i] == "--token") token = value;
}

await using var simulator = await StreamerSongListSimulator.StartAsync(new StreamerSongListSimulatorOptions
{
    Port = port,
    AccessToken = token,
    LogToConsole = true
});
if (seed == "demo") await DemoSeed.ApplyAsync(simulator, TimeProvider.System);

Console.WriteLine($"""
    StreamerSongList simulator listening on {simulator.ApiBaseAddress}
    Point the app at it (PowerShell), then start the app from the same shell:
      $env:SONGLISTSPINNER_SSL_API_BASE_URL = "{simulator.ApiBaseAddress}"
      $env:SONGLISTSPINNER_SSL_EVENTS_URL = "{simulator.EventsEndpoint}"
      $env:SONGLISTSPINNER_SSL_ACCESS_TOKEN = "{token}"
      $env:SONGLISTSPINNER_SSL_TOKEN_TYPE = "streamer"
    and keep your own settings, saved token and overlay port out of it with a test profile:
      $env:SONGLISTSPINNER_PROFILE_DIR = "$env:TEMP\songlistspinner-sim"
      $env:SONGLISTSPINNER_OVERLAY_PORT = "5151"
      $env:SONGLISTSPINNER_UPDATE_RELEASE_URL = "{simulator.ApiBaseAddress}_simulator/no-releases"
    Without the profile, a token saved in Settings is sent instead (and rejected), and Settings saves into
    your real profile.
    {(seed == "demo" ? $"Seeded channel: '{DemoSeed.ChannelName}' on twitch (streamer {DemoSeed.StreamerId})." : "No channels seeded.")}
    Add a request:  POST {simulator.ApiBaseAddress}_simulator/requests?streamer_id=<id>&artist=<a>&title=<t>&requester=<r>
    Drop events:    POST {simulator.ApiBaseAddress}_simulator/events/drop
    Press Ctrl+C to stop.
    """);

var stopped = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopped.TrySetResult();
};
await stopped.Task;
return 0;
