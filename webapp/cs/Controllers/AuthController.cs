using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController 
    {
        private readonly IConfiguration configuration;
        private readonly HttpContext httpContext;

        public AuthController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            this.configuration = configuration;
            httpContext = httpContextAccessor.HttpContext;
        }

        [HttpGet]
        public async Task<ActionResult> GetAuth()
        {
            var str = configuration.GetConnectionString("Isucon9");
            try
            {
                var user = await GetUser(httpContext, str);
                return new OkObjectResult(new AuthResponseModel
                {
                    Email = user.Email
                });
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message)
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        internal static async Task<UserModel> GetUser(HttpContext httpContext, string connectionString)
        {
            long userID;
            try
            {
                userID = BitConverter.ToInt64(httpContext.Session.Get("user_id"));
            }
            catch (Exception e)
            {
                throw new Exception("no session", e);
            }
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            try
            {
                var user = await connection.QueryFirstOrDefaultAsync<UserModel>(
                    "SELECT * FROM `users` WHERE `id` = @id",
                new { id = userID });
                if (user == null)
                    throw new Exception("user not found");
                return user;
            }
            catch (Exception e)
            {
                throw new Exception("db error", e);
            }
        }

        [HttpPost("Signup")]
        public async Task<ActionResult> Signup(UserModel user)
        {
            var salt = new byte[1024];
            using (var rng = new RNGCryptoServiceProvider())
            {
                try
                {
                    rng.GetBytes(salt);
                }
                catch (Exception)
                {
                    return new ObjectResult("salt generator error")
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }
            }
            var b = new Rfc2898DeriveBytes(user.Password, salt, 100, HashAlgorithmName.SHA256);
            var superSecurePassword = b.GetBytes(256);
            var str = configuration.GetConnectionString("Isucon9");
            using (var connection = new MySqlConnection(str))
            {
                connection.Open();
                try
                {
                    await connection.ExecuteAsync("INSERT INTO `users` (`email`, `salt`, `super_secure_password`) VALUES (@email, @salt, @superSecurePassword)",
                    new { email = user.Email, salt, superSecurePassword });
                }
                catch (Exception)
                {
                    return new ObjectResult("user registration failed")
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }
            }
            return new OkObjectResult("registration complete");
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserModel postUser)
        {
            Console.WriteLine($"login... {postUser.Email} {postUser.Password}");
            var str = configuration.GetConnectionString("Isucon9");
            Console.WriteLine($"str {str}");
            using (var connection = new MySqlConnection(str))
            {
                connection.Open();
                var user = await connection.QueryFirstOrDefaultAsync<UserModel>("SELECT * FROM users WHERE email=@email", new {email=postUser.Email});
                Console.WriteLine($"user. {user}");
                if (user == null)
                {
                    return new ForbidResult("authentication failed");
                }
                var b = new Rfc2898DeriveBytes(postUser.Password, user.Salt, 100, HashAlgorithmName.SHA256);
                var challengePassword = b.GetBytes(256);
                if (!(user.SuperSecurePassword?.SequenceEqual(challengePassword) == true))
                {
                    return new ForbidResult("authentication failed");
                }
                try
                {
                    Console.WriteLine($"get user {user.SuperSecurePassword}");
                    httpContext.Session.Set("user_id", BitConverter.GetBytes(user.ID));
                    await httpContext.Session.CommitAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return new ObjectResult("session error")
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }
            }
            return new OkObjectResult("autheticated");
        }

        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            try
            {
                httpContext.Session.Set("user_id", BitConverter.GetBytes(0L));
                await httpContext.Session.CommitAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new ObjectResult("session error")
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
            return new OkObjectResult("autheticated");
        }
    }

    public class UserModel
    {
        [JsonPropertyName("ID")]
        public long ID { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonIgnore]
        public byte[] Salt { get; set; }
        [JsonIgnore]
        public byte[] SuperSecurePassword { get; set; }
    }

    public class AuthResponseModel
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}
