namespace SonglistSpinner.Core.Api.V2;

public sealed class StreamerSongListApiOptions
{
    public static readonly Uri ProductionBaseAddress =
        new("https://api.streamersonglist.com/", UriKind.Absolute);

    public Uri BaseAddress { get; init; } = ProductionBaseAddress;
    public int PageSize { get; init; } = 100;
}
