namespace SonglistSpinner.Core.Api.V2;

public sealed class StreamerSongListApiOptions
{
    public static readonly Uri StagingBaseAddress =
        new("https://api.staging.streamersonglist.com/", UriKind.Absolute);

    public Uri BaseAddress { get; init; } = StagingBaseAddress;
    public int PageSize { get; init; } = 200;
}
