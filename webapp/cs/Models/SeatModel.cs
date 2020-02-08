using System.Text.Json.Serialization;

namespace cs.Models
{
    public class SeatModel
    {
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seat_column")]
        public string SeatColumn { get; set; }
        [JsonPropertyName("seat_row")]
        public int SeatRow { get; set; }
        [JsonPropertyName("seat_class")]
        public string SeatClass { get; set; }
        [JsonPropertyName("is_smoking_seat")]
        public bool IsSmokingSeat { get; set; }
    }
}
