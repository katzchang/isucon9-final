using System.Text.Json.Serialization;

namespace cs.Models
{
    public class SeatReservationModel
    {
        //TODO omit if null
        [JsonPropertyName("reservation_id")]
        public int ReservationId { get; set; }
        //TODO omit if null
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seat_row")]
        public int SeatRow { get; set; }
        [JsonPropertyName("seat_column")]
        public int SeatColumn { get; set; }
    }
}