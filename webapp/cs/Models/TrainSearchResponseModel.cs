using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace cs.Models
{
    public class TrainSearchResponseModel
    {
        [JsonPropertyName("train_class")]
        public string Class { get; set; }
        [JsonPropertyName("train_name")]
        public string Name { get; set; }
        [JsonPropertyName("start")]
        public string Start { get; set; }
        [JsonPropertyName("last")]
        public string Last { get; set; }
        [JsonPropertyName("departure")]
        public string Departure { get; set; }
        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }
        [JsonPropertyName("departure_time")]
        public string DepartureTime { get; set; }
        [JsonPropertyName("arrival_time")]
        public string ArrivalTime { get; set; }
        [JsonPropertyName("seat_availability")]
        public Dictionary<string, string> SeatAvailability { get; set; }
        [JsonPropertyName("seat_fare")]
        public Dictionary<string, int> Fare { get; set; }
    }
}
