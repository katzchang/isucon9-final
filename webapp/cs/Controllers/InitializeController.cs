using cs.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
                AvailableDays = Utils.AvailableDates,
                Language = Utils.Language
            };
        }
    }
}
