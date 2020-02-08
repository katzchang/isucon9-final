using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class PaymentResponseModel
    {
        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; }
        [JsonPropertyName("is_ok")]
        public bool IsOk { get; set; }
    }
}
