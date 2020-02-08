using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class PaymentInformationModel
    {
        [JsonPropertyName("payment_information")]
        public PaymentInformationRequestModel PayInfo { get; set; }
    }
}
