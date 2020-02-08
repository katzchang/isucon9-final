using System.Text.Json.Serialization;

namespace cs.Models
{
    public class InitializeResponse
    {
        [JsonPropertyName("available_days")]
        public int AvailableDays { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; set; }
    }
}