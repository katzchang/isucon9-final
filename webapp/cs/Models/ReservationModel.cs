using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class ReservationModel
    {
        [JsonPropertyName("reservation_id")]
        public int ReservationId { get; set; }
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("departure")]
        public string Departure { get; set; }
        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }
        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        //ommit empty
        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; }
        [JsonPropertyName("adult")]
        public int Adult { get; set; }
        [JsonPropertyName("child")]
        public int Child { get; set; }
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }
}
