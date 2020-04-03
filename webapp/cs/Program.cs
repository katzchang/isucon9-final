using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace cs
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                //環境変数から接続文字列を生成して設定する
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var host = Environment.GetEnvironmentVariable("MYSQL_HOSTNAME");
                    host ??= "127.0.0.1";
                    var port = Environment.GetEnvironmentVariable("MYSQL_PORT");
                    if (!int.TryParse(port, out _))
                        port = null;
                    port ??= "3306";
                    var user = Environment.GetEnvironmentVariable("MYSQL_USER");
                    user ??= "isutrain";
                    var dbname = Environment.GetEnvironmentVariable("MYSQL_DATABASE");
                    dbname ??= "isutrain";
                    var password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD");
                    password ??= "isutrain";
                    var connectionString = $"Server={host};Port={port};Database={dbname};Uid={user};Pwd={password};Charset=utf8mb4";
                    config.AddInMemoryCollection(new[]
                    {
                        KeyValuePair.Create("ConnectionStrings:Isucon9", connectionString)
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
