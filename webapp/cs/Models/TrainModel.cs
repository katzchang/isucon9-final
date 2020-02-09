using System;
using System.Text.Json.Serialization;

namespace cs.Models
{
    public class TrainModel
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        [JsonPropertyName("departure_at")]
        public TimeSpan DepartureAt { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("start_station")]
        public string StartStation { get; set; }
        [JsonPropertyName("last_station")]
        public string LastStation { get; set; }
        [JsonPropertyName("is_nobori")]
        public bool IsNobori { get; set; }
    }
}
