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
    [Route("api/[controller]")]
    public class TrainController
    {
        private readonly IConfiguration configuration;

        public TrainController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpGet("Search")]
        public async Task<IEnumerable<TrainSearchResultModel>> Search()
        {
            return null;
        }

        [HttpGet("Seat")]
        public SettingModel Seat()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }

        [HttpPost("reserve")]
        public SettingModel Reserve()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }

        [HttpPost("reserve/commit")]
        public SettingModel CommitReserve()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }
    }

    public class TrainSearchResultModel
    {
        [JsonPropertyName("payment_api")]
        public string PaymentAPI { get; set; }
    }
}
