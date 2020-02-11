using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class ReservationResponseModel
    {
        [JsonPropertyName("reservation_id")]
        public int ReservationId { get; set; }
        [JsonPropertyName("date")]
        public string Date { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seat_class")]
        public string SeatClass { get; set; }
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
        [JsonPropertyName("adult")]
        public int Adult { get; set; }
        [JsonPropertyName("child")]
        public int Child { get; set; }
        [JsonPropertyName("departure")]
        public string Departure { get; set; }
        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }
        [JsonPropertyName("departure_time")]
        public string DepartureTime { get; set; }
        [JsonPropertyName("arrival_time")]
        public string ArrivalTime { get; set; }
        [JsonPropertyName("seats")]
        public SeatReservationModel[] Seats { get; set; }

    }
}
