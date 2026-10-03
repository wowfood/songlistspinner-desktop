namespace SonglistSpinner.Simulator;

public sealed class StreamerSongListSimulatorOptions
{
    public const string DefaultAccessToken = "simulator-token";

    /// <summary>The loopback port to listen on; 0, the default, takes any free port.</summary>
    public int Port { get; init; }

    /// <summary>The one token the REST API accepts, under any of the <c>Streamer</c>, <c>User</c> and <c>Bearer</c> schemes.</summary>
    public string AccessToken { get; init; } = DefaultAccessToken;

    /// <summary>The clock that stamps songs marked played through the API or the scenario API.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Writes ASP.NET Core's request log to the console. Off when hosted in a test.</summary>
    public bool LogToConsole { get; init; }
}
