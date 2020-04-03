using cs.Controllers;
using cs.Models;
using Dapper;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace cs
{
    public static class Utils
    {
        public static readonly string Banner = "ISUTRAIN API";
        public static readonly IReadOnlyDictionary<string, string> TrainClassMap
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                {"express", "最速"},
                {"semi_express", "中間"},
                {"local", "遅いやつ"}
            });
        public static readonly int AvailableDates = 10;
        //クロスプラットフォームで同じタイムゾーン識別IDを使うためにTimeZoneConverterを利用
        //https://tech.tanaka733.net/entry/2020/02/timezone-id
        public static readonly TimeZoneInfo TokyoStandardTimeZone = TimeZoneConverter.TZConvert.GetTimeZoneInfo("Asia/Tokyo");
        public static readonly string Language = "C#";

        public static bool CheckAvailableDate(DateTimeOffset date)
        {
            var t = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TokyoStandardTimeZone.BaseUtcOffset).AddDays(AvailableDates);
            return date < t;
        }

        public static string[] GetUsableTrainClassList(StationModel fromStation, StationModel toStation)
        {
            var usable = TrainClassMap.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (!fromStation.IsStopExpress)
            {
                usable.Remove("express");
            }
            if (!fromStation.IsStopSemiExpress)
            {
                usable.Remove("semi_express");
            }
            if (!fromStation.IsStopLocal)
            {
                usable.Remove("local");
            }
            if (!toStation.IsStopExpress)
            {
                usable.Remove("express");
            }
            if (!toStation.IsStopSemiExpress)
            {
                usable.Remove("semi_express");
            }
            if (!toStation.IsStopLocal)
            {
                usable.Remove("local");
            }
            return usable.Values.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="connection"></param>
        /// <param name="txn"></param>
        /// <returns></returns>
        /// <exception cref="HttpResponseException"/>
        public static async Task<UserModel> GetUser(HttpContext httpContext, MySqlConnection connection, MySqlTransaction txn = null)
        {
            long userID;
            try
            {
                userID = BitConverter.ToInt64(httpContext.Session.Get("user_id"));
            }
            catch (Exception e)
            {
                if (txn != null) await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status401Unauthorized,"no session", e);
            }
            try
            {
                var user = await connection.QueryFirstOrDefaultAsync<UserModel>(
                    "SELECT * FROM `users` WHERE `id` = @id",
                new { id = userID });
                if (user == null)
                {
                    if (txn != null) await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status401Unauthorized, "user not found");
                }
                return user;
            }
            catch (Exception e)
            {
                if (txn != null) await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "db error", e);
            }
        }
    }
}
