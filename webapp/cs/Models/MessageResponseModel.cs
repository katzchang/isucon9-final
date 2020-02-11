using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Models
{
    public class MessageResponseModel
    {
        public MessageResponseModel() { }
        public MessageResponseModel(string message, bool isError = false)
        {
            Message = message;
            IsError = isError;
        }
        [JsonPropertyName("is_error")]
        public bool IsError { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
