using System.Text.Json.Serialization;

namespace cs.Models
{
    public class AuthResponseModel
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}