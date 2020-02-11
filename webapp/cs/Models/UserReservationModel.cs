using System.Text.Json.Serialization;

namespace cs.Models
{
    public class UserReservationModel
    {
        [JsonPropertyName("payment_api")]
        public string PaymentAPI { get; set; }
    }
}