using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
        public SettingModel List()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel{ PaymentAPI = api };
        }

        [HttpGet("/{id}")]
        public SettingModel Get(long id)
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }

        [HttpPost("/{id}/cancel")]
        public SettingModel Cancel(long id)
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }

    }

    public class UserReservationModel
    {
        [JsonPropertyName("payment_api")]
        public string PaymentAPI { get; set; }
    }
}
