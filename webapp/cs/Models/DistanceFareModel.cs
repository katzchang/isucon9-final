using System.Text.Json.Serialization;

namespace cs.Models
{
    public class DistanceFareModel
    {
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        [JsonPropertyName("fare")]
        public int Fare { get; set; }
    }
}