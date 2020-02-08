using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class SeatInformationByCarNumberModel
    {
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seats")]
        public SeatInformationModel[] SeatInformationList { get; set; }
    }
}
