using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class ReservationPaymentResponseModel
    {
        [JsonPropertyName("is_ok")]
        public bool IsOk { get; set; }
    }
}
