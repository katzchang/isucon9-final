using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace cs
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
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
            var connectionString = $"server={host}:{port};database={dbname};uid={user};pwd={password};charset=utf8mb4";
            Configuration = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddInMemoryCollection(new[]
                {
                    KeyValuePair.Create("ConnectionStrings:Isucon9", connectionString)
                })
                .Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.Configure<RouteOptions>(options => {
                // URL‚ð¬•¶Žš‚É‚·‚é
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Console.WriteLine(Configuration.GetConnectionString("Isucon9"));
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => 
            {
                endpoints.MapControllerRoute("default", "api/{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
