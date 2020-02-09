using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dapper;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/user/reservations")]
    public class UserReservationController
    {
        private readonly IConfiguration configuration;

        public UserReservationController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult> List()
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            return null;
        }

        [HttpGet("/{id}")]
        public async Task<ActionResult> Get(long id)
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            return null;
        }

        [HttpPost("/{id}/cancel")]
        public async Task<ActionResult> Cancel(long id)
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            return null;
        }

    }

    public class UserReservationModel
    {
        [JsonPropertyName("payment_api")]
        public string PaymentAPI { get; set; }
    }
}
