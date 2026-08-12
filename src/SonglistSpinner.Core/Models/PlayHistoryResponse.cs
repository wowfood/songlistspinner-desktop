using System.Text.Json.Serialization;

namespace SonglistSpinner.Core.Models;

public class PlayHistoryResponse
{
    [JsonPropertyName("items")] public List<PlayHistoryItem> Items { get; set; } = [];
}