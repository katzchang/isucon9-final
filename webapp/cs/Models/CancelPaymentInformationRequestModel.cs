using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class CancelPaymentInformationRequestModel
    {
        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; }
    }
}
