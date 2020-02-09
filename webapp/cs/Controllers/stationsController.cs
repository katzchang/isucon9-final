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
        [JsonPropertyName("id")]
        public int ID { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        [JsonPropertyName("is_stop_express")]
        public bool IsStopExpress { get; set; }
        [JsonPropertyName("is_stop_semi_express")]
        public bool IsStopSemiExpress { get; set; }
        [JsonPropertyName("is_stop_local")]
        public bool IsStopLocal { get; set; }
        public override string ToString() => $"{{{ID} {Name} {Distance} {IsStopExpress} {IsStopSemiExpress} {IsStopLocal}}}";
    }
}
