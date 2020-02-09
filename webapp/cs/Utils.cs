using cs.Controllers;
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
        //TODO Unixでしか動かない
        public static readonly TimeZoneInfo TokyoStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        public static readonly string Language = "C#";

        public static bool CheckAvailableDate(DateTimeOffset date)
        {
            var t = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TokyoStandardTimeZone.BaseUtcOffset);
            t.AddDays(AvailableDates);
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
            return usable.Keys.ToArray();
        }
    }
}
