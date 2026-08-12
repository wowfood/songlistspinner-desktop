using System.Text.Json.Serialization;

namespace SonglistSpinner.Core.Api.V2;

internal sealed class QueueResponseDto
{
    [JsonPropertyName("items")] public List<QueueDetailsDto> Items { get; init; } = [];
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed class QueueDetailsDto
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("position")] public int Position { get; init; }
    [JsonPropertyName("nonlistSong")] public string? NonlistSong { get; init; }
    [JsonPropertyName("requests")] public List<RequestDto> Requests { get; init; } = [];
    [JsonPropertyName("song")] public QueueSongDto? Song { get; init; }
    [JsonPropertyName("songId")] public int? SongId { get; init; }
}

internal sealed class QueueSongDto
{
    [JsonPropertyName("artist")] public string? Artist { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
}

internal sealed class RequestDto
{
    [JsonPropertyName("amount")] public decimal? Amount { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("user")] public RequestUserDto? User { get; init; }
}

internal sealed class RequestUserDto
{
    [JsonPropertyName("username")] public string? Username { get; init; }
}

internal sealed class PlayHistoryResponseDto
{
    [JsonPropertyName("items")] public List<PlayHistoryDetailsDto> Items { get; init; } = [];
    [JsonPropertyName("token")] public string? Token { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed class PlayHistoryDetailsDto
{
    [JsonPropertyName("donationAmount")] public decimal? DonationAmount { get; init; }
    [JsonPropertyName("requests")] public List<RequestDto> Requests { get; init; } = [];
    [JsonPropertyName("song")] public PlayHistorySongDto? Song { get; init; }
    [JsonPropertyName("songId")] public int? SongId { get; init; }
}

internal sealed class PlayHistorySongDto
{
    [JsonPropertyName("artist")] public string? Artist { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
}
