using System.Text.Json.Serialization;

namespace SonglistSpinner.Core.StreamerSongList;

public class SpinnerRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("donationAmount")] public decimal? DonationAmount { get; set; }

    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
}
