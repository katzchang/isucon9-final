using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class ReservationPaymentRequestModel
    {
        [JsonPropertyName("card_token")]
        public string CardToken { get; set; }
        [JsonPropertyName("reservation_id")]
        public int ReservationId { get; set; }

    }
}
