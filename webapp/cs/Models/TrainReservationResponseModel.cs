using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class TrainReservationResponseModel
    {
        [JsonPropertyName("reservation_id")]
        public long ReservationId { get; set; }
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
        [JsonPropertyName("is_ok")]
        public bool IsOk { get; set; }
    }
}
