using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class CarInformationModel
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }
        [JsonPropertyName("seats")]
        public SeatInformationModel[] SeatInformationList { get; set; }
        [JsonPropertyName("cars")]
        public SimpleCarInformationModel[] Cars { get; set; }
    }
}
