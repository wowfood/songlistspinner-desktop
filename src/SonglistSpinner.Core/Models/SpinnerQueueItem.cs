using System.Text.Json.Serialization;

namespace SonglistSpinner.Core.Models;

public class SpinnerQueueItem
{
    [JsonPropertyName("queueId")] public int QueueId { get; set; }

    [JsonPropertyName("song")] public SpinnerSong Song { get; set; } = new();

    [JsonPropertyName("requests")] public List<SpinnerRequest> Requests { get; set; } = [];

    [JsonPropertyName("position")] public int Position { get; set; }
}
