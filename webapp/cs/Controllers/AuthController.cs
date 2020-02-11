using cs.Models;
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
        public async Task<AuthResponseModel> GetAuth()
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            await connection.OpenAsync();
            var user = await Utils.GetUser(httpContext, connection);
            return new AuthResponseModel
            {
                Email = user.Email
            };
        }

        [HttpPost("Signup")]
        public async Task<MessageResponseModel> Signup(UserModel user)
        {
            var salt = new byte[1024];
            using (var rng = new RNGCryptoServiceProvider())
            {
                try
                {
                    rng.GetBytes(salt);
                }
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "salt generator error", e);
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
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "user registration failed", e);
                }
            }
            return new MessageResponseModel("registration complete");
        }

        [HttpPost("login")]
        public async Task<MessageResponseModel> Login(UserModel postUser)
        {
            Console.WriteLine($"login... {postUser.Email} {postUser.Password}");
            var str = configuration.GetConnectionString("Isucon9");
            Console.WriteLine($"str {str}");
            using (var connection = new MySqlConnection(str))
            {
                connection.Open();
                var user = await connection.QueryFirstOrDefaultAsync<UserModel>("SELECT * FROM users WHERE email=@email", new { email = postUser.Email });
                Console.WriteLine($"user. {user}");
                if (user == null)
                {
                    throw new HttpResponseException(StatusCodes.Status403Forbidden, "authentication failed");
                }
                var b = new Rfc2898DeriveBytes(postUser.Password, user.Salt, 100, HashAlgorithmName.SHA256);
                var challengePassword = b.GetBytes(256);
                if (!(user.SuperSecurePassword?.SequenceEqual(challengePassword) == true))
                {
                    throw new HttpResponseException(StatusCodes.Status403Forbidden, "authentication failed");
                }
                try
                {
                    Console.WriteLine($"get user {user.SuperSecurePassword}");
                    httpContext.Session.Set("user_id", BitConverter.GetBytes(user.ID));
                    await httpContext.Session.CommitAsync();
                }
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "session error", e);
                }
            }
            return new MessageResponseModel("autheticated");
        }

        [HttpPost("logout")]
        public async Task<MessageResponseModel> Logout()
        {
            try
            {
                httpContext.Session.Set("user_id", BitConverter.GetBytes(0L));
                await httpContext.Session.CommitAsync();
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "session error", e);
            }
            return new MessageResponseModel("logged out");
        }
    }

    
}