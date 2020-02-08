using System;
using System.Text.Json.Serialization;

namespace cs.Models
{
    public class FareModel
    {
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("seat_class")]
        public string SeatClass { get; set; }
        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }
        [JsonPropertyName("fare_multiplier")]
        public double FareMultiplier { get; set; }
    }
}
