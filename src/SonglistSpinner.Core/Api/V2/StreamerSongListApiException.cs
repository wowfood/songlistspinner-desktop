using System.Net;

namespace SonglistSpinner.Core.Api.V2;

public sealed class StreamerSongListApiException : Exception
{
    public StreamerSongListApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
