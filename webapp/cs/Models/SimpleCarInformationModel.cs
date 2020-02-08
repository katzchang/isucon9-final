using System.Text.Json.Serialization;

namespace cs.Models
{
    public class SimpleCarInformationModel
    {
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seat_class")]
        public string SeatClass { get; set; }
    }
}