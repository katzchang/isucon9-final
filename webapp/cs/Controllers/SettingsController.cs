using cs.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController
    {
        public SettingModel Get()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return new SettingModel { PaymentAPI = api };
        }
    }
}
