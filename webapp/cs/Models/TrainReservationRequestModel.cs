using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class TrainReservationRequestModel
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("is_smoking_seat")]
        public bool IsSmokingSeat { get; set; }
        [JsonPropertyName("seat_class")]
        public string SeatClass { get; set; }
        [JsonPropertyName("departure")]
        public string Departure { get; set; }
        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }
        [JsonPropertyName("adult")]
        public int Adult { get; set; }
        [JsonPropertyName("child")]
        public int Child { get; set; }
        [JsonPropertyName("Column")] //TODOここだけ大文字?
        public string Column { get; set; }
        [JsonPropertyName("seats")] //TODOここだけ大文字?
        public RequestSeatModel Seats { get; set; }
    }
}
