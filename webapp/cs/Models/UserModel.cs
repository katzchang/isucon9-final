using System.Text.Json.Serialization;

namespace cs.Models
{
    public class UserModel
    {
        [JsonPropertyName("ID")]
        public long ID { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonIgnore]
        public byte[] Salt { get; set; }
        [JsonIgnore]
        public byte[] SuperSecurePassword { get; set; }
    }
}