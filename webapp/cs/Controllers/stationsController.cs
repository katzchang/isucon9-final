using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StationsController
    {
        private readonly IConfiguration configuration;

        public StationsController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpGet]
        public async Task<IEnumerable<StationModel>> List()
        {
            var str = configuration.GetConnectionString("Isucon9");
            using (var connection = new MySqlConnection(str))
            {
                connection.Open();
                return (await connection.QueryAsync<StationModel>("SELECT * FROM station_master ORDER BY id")).ToArray();
            }
        }
    }

    public class StationModel
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        [JsonPropertyName("departure_at")]
        public string DepartureAt { get; set; }
        [JsonPropertyName("train_class")]
        public string TrainClass { get; set; }
        [JsonPropertyName("train_name")]
        public string TrainName { get; set; }
        [JsonPropertyName("start_station")]
        public string StartStation { get; set; }
        [JsonPropertyName("last_station")]
        public string LastStation { get; set; }
        [JsonPropertyName("is_nobori")]
        public bool IsNobori { get; set; }
    }
}
