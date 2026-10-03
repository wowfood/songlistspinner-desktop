using System.Text.Json.Serialization;

namespace SonglistSpinner.Services;

// One wheel slice. QueueId lets the overlay find the winner by queue entry; placeholder and preview
// slices have none, and then the property is left out of the JSON.
public sealed record OverlayWheelItem(string Label)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? QueueId { get; init; }
}
