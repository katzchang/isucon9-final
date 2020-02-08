using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class SeatInformationModel
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }
        [JsonPropertyName("column")]
        public string Column { get; set; }
        [JsonPropertyName("class")]
        public string Class { get; set; }
        [JsonPropertyName("is_smoking_seat")]
        public bool IsSmokingSeat { get; set; }
        [JsonPropertyName("is_occupied")]
        public bool IsOccupied { get; set; }
    }
}
