using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InitializeController
    {
        private readonly IConfiguration configuration;

        public InitializeController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpPost]
        public async Task<InitializeResponse> Initialize()
        {
            var str = configuration.GetConnectionString("Isucon9");
            using (var connection = new MySqlConnection(str))
            {
                connection.Open();
                await connection.ExecuteAsync("TRUNCATE seat_reservations");
                await connection.ExecuteAsync("TRUNCATE reservations");
                await connection.ExecuteAsync("TRUNCATE users");
            }
            return new InitializeResponse
            {
                AvailableDays = 10,
                Language = "C#"
            };
        }
    }

    public class InitializeResponse
    {
        [JsonPropertyName("available_days")]
        public int AvailableDays { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; set; }
    }
}
