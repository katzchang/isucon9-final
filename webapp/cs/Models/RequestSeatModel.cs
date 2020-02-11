using System.Text.Json.Serialization;

namespace cs.Models
{
    public class RequestSeatModel
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }
        [JsonPropertyName("column")] //TODOここだけ大文字?
        public string Column { get; set; }

        public override string ToString() => $"{{{Row} {Column}}}";
    }
}