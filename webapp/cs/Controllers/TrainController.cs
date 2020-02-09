using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using cs.Models;

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

        /// <summary>
        /// 列車検索
        /// GET /train/search? use_at =< ISO8601形式の時刻 > &from = 東京 & to = 大阪
        /// </summary>
        /// <param name="use_at"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns>
        /// 料金
        /// 空席情報
        ///
        /// 発駅と着駅の到着時刻
        /// </returns>
        [HttpGet("search")]
        public async Task<ActionResult> Search(
            [FromQuery(Name = "use_at")]string useAt, [FromQuery]string from, [FromQuery]string to,
            [FromQuery(Name = "train_class")]string trainClass, [FromQuery]int adult, [FromQuery]int child)
        {
            Console.WriteLine("search...");
            DateTimeOffset date;
            try
            {
                var d = DateTimeOffset.Parse(useAt);
                date = TimeZoneInfo.ConvertTime(d, Utils.TokyoStandardTimeZone);
//                date = new DateTimeOffset(d.ToUniversalTime(), Utils.TokyoStandardTimeZone.BaseUtcOffset);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new BadRequestObjectResult(e);
            }
Console.WriteLine($"date is {date}");
            if (!Utils.CheckAvailableDate(date))
            {
                return new NotFoundObjectResult("予約可能期間外です");
            }

            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);

            async Task<StationModel> GetStation(string name)
            {
                var query = "SELECT * FROM station_master WHERE name=@name";
                return await connection.QuerySingleAsync<StationModel>(query, new { name });
            }
            StationModel fromStation;
            StationModel toStation;
            try
            {
                fromStation = await GetStation(from);
                if (fromStation == null)
                {
                    Console.WriteLine("fromStation: no rows");
                    return new BadRequestObjectResult("fromStation: no rows");
                }
            }
            catch (Exception e)
            {
                return new ObjectResult(e) { StatusCode = StatusCodes.Status500InternalServerError };
            }
            try
            {
                toStation = await GetStation(to);
                if (toStation == null)
                {
                    Console.WriteLine("toStation: no rows");
                    return new BadRequestObjectResult("toStation: no rows");
                }
            }
            catch (Exception e)
            {
                return new ObjectResult(e) { StatusCode = StatusCodes.Status500InternalServerError };
            }

            var isNobori = fromStation.Distance > toStation.Distance;

            var usableTrainClassList = Utils.GetUsableTrainClassList(fromStation, toStation);
            var query = $"SELECT * FROM train_master WHERE date=@Date AND train_class IN @usableTrainClassList AND is_nobori=@isNobori{(string.IsNullOrEmpty(trainClass) ? "" : " AND train_class=@trainClass")}";
            var trainList = await connection.QueryAsync<TrainModel>(query, new { date.Date, usableTrainClassList, isNobori, trainClass });

            query = $"SELECT * FROM station_master ORDER BY distance{(isNobori ? " DESC" : "")}";
            var stations = await connection.QueryAsync<StationModel>(query);

            Console.WriteLine($"From {fromStation}");
            Console.WriteLine($"To {toStation}");

            var trainSearchResponseList = new List<TrainSearchResponseModel>();

            foreach (var train in trainList)
            {
                var isSeekedToFirstStation = false;
                var isContainsOriginStation = false;
                var isContainsDestStation = false;
                var i = 0;

                foreach (var station in stations)
                {
                    if (!isSeekedToFirstStation)
                    {
                        // 駅リストを列車の発駅まで読み飛ばして頭出しをする
                        // 列車の発駅以前は止まらないので無視して良い
                        if (station.Name == train.StartStation)
                        {
                            isSeekedToFirstStation = true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (station.ID == fromStation.ID)
                    {
                        // 発駅を経路中に持つ編成の場合フラグを立てる
                        isContainsOriginStation = true;
                    }
                    if (station.ID == toStation.ID)
                    {
                        if (isContainsOriginStation)
                        {
                            // 発駅と着駅を経路中に持つ編成の場合
                            isContainsDestStation = true;
                            break;
                        }
                        else
                        {
                            // 出発駅より先に終点が見つかったとき
                            Console.WriteLine("なんかおかしい");
                            break;
                        }
                    }
                    if (station.Name == train.LastStation)
                    {
                        // 駅が見つからないまま当該編成の終点に着いてしまったとき
                        break;
                    }
                    i++;
                }

                if (isContainsOriginStation && isContainsDestStation)
                {
                    // 列車情報

                    try
                    {
                        // 所要時間
                        var departure = await connection.QuerySingleAsync<string>(
                            "SELECT departure FROM train_timetable_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName AND station=@Name",
                            new { date.Date, train.TrainClass, train.TrainName, fromStation.Name });
                        var departureDate = DateTimeOffset.Parse($"{date.ToString("yyyy/MM/dd")} {departure} +09:00");
                        if (!(date < departureDate))
                        {
                            // 乗りたい時刻より出発時刻が前なので除外
                            continue;
                        }
                        var arrival = await connection.QuerySingleAsync<string>(
                            "SELECT arrival  FROM train_timetable_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName AND station=@Name",
                            new { date.Date, train.TrainClass, train.TrainName, toStation.Name });

                        async Task<SeatModel[]> GetAvailableSeats(TrainModel train,
                            StationModel fromStation, StationModel toStation, string seatClass, bool isSmokingSeat)
                        {
                            // 全ての座席を取得する
                            var q = "SELECT * FROM seat_master WHERE train_class=@TrainClass AND seat_class=@seatClass AND is_smoking_seat=@isSmokingSeat";
                            var seatList = await connection.QueryAsync<SeatModel>(q,
                                new { train.TrainClass, seatClass, isSmokingSeat });
                            var availableSeatMap = seatList.ToDictionary(s => $"{s.CarNumber:d}_{s.SeatRow:d}_{s.SeatColumn}", s => s);

                            var q2 = @"
SELECT sr.reservation_id, sr.car_number, sr.seat_row, sr.seat_column 
FROM seat_reservations sr, reservations r, seat_master s, station_master std, station_master sta 
WHERE 
	r.reservation_id=sr.reservation_id AND 
	s.train_class=r.train_class AND 
	s.car_number=sr.car_number AND 
	s.seat_column=sr.seat_column AND 
	s.seat_row=sr.seat_row AND 
	std.name=r.departure AND 
	sta.name=r.arrival 
" + (isNobori ?
"AND ((sta.id < @fromStationID AND @fromStationID <= std.id) OR (sta.id < @toStationID AND @toStationID <= std.id) OR (@fromStationID < sta.id AND std.id < @toStationID))" :
"AND ((std.id <= @fromStationID AND @fromStationID < sta.id) OR (std.id <= @toStationID AND @toStationID < sta.id) OR (sta.id < @fromStationID AND @toStationID < std.id))");

                            var seatReservationList = await connection.QueryAsync<SeatModel>(q2,
                                new { fromStationID = fromStation.ID, toStationID = toStation.ID });

                            foreach (var seatReservation in seatReservationList)
                            {
                                availableSeatMap.Remove($"{seatReservation.CarNumber:d}_{seatReservation.SeatRow:d}_{seatReservation.SeatColumn}");
                            }
                            return availableSeatMap.Values.ToArray();
                        }

                        try
                        {
                            var premiumAvailSeats = await GetAvailableSeats(train, fromStation, toStation, "premium", false);
                            var premiumSmokeAvailSeats = await GetAvailableSeats(train, fromStation, toStation, "premium", true);
                            var reservedAvailSeats = await GetAvailableSeats(train, fromStation, toStation, "reserved", false);
                            var reservedSmokeAvailSeats = await GetAvailableSeats(train, fromStation, toStation, "reserved", true);

                            static string ToAvailabilityString(SeatModel[] seats)
                            {
                                return seats.Length switch
                                {
                                    0 => "×",
                                    var i when i < 10 => "△",
                                    _ => "○"
                                };
                            }
                            var premiumAvail = ToAvailabilityString(premiumAvailSeats);
                            var premiumSmokeAvail = ToAvailabilityString(premiumSmokeAvailSeats);
                            var reservedAvail = ToAvailabilityString(reservedAvailSeats);
                            var reservedSmokeAvail = ToAvailabilityString(reservedSmokeAvailSeats);

                            // 空席情報
                            var seatAvailability = new Dictionary<string, string>
                            {
                                {"premium", premiumAvail},
                                {"premium_smoke", premiumSmokeAvail},
                                {"reserved", reservedAvail},
                                {"reserved_smoke", reservedSmokeAvail},
                                {"premium", "○"}
                            };

                            // 料金計算
                            int premiumFare, reservedFare, nonReservedFare;
                            try
                            {
                                premiumFare = await FareCalc(date, fromStation.ID, toStation.ID, train.TrainClass, "premium", connection);
                                premiumFare = premiumFare * adult + premiumFare / 2 * child;
                                reservedFare = await FareCalc(date, fromStation.ID, toStation.ID, train.TrainClass, "premium", connection);
                                reservedFare = reservedFare * adult + reservedFare / 2 * child;
                                nonReservedFare = await FareCalc(date, fromStation.ID, toStation.ID, train.TrainClass, "premium", connection);
                                nonReservedFare = nonReservedFare * adult + nonReservedFare / 2 * child;
                            }
                            catch (Exception e)
                            {
                                return new ObjectResult(e) { StatusCode = StatusCodes.Status400BadRequest };
                            }
                            var fareInformation = new Dictionary<string, int>
                            {
                                {"premium", premiumFare },
                                {"premium_smoke", premiumFare },
                                {"reserved", reservedFare },
                                {"reserved_smoke", reservedFare },
                                {"non_reserved", nonReservedFare },
                            };

                            trainSearchResponseList.Add(new TrainSearchResponseModel
                            {
                                Class = train.TrainClass, 
                                Name = train.TrainName,
                                Start = train.StartStation,
                                Last = train.LastStation,
                                Departure = fromStation.Name,
                                Arrival = toStation.Name,
                                DepartureTime = departure,
                                ArrivalTime = arrival,
                                SeatAvailability = seatAvailability,
                                Fare = fareInformation
                            });

                            if (trainSearchResponseList.Count() >= 10)
                            {
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            return new ObjectResult(e) { StatusCode = StatusCodes.Status400BadRequest };
                        }
                    }
                    catch (Exception e)
                    {
                        return new ObjectResult(e) { StatusCode = StatusCodes.Status500InternalServerError };
                    }
                }
            }
            return new OkObjectResult(trainSearchResponseList);
        }

        /// <summary>
        /// 指定した列車の座席列挙
        /// GET /train/seats?date=2020-03-01&train_class=のぞみ&train_name=96号&car_number=2&from=大阪&to=東京
        /// </summary>
        /// <returns></returns>
        [HttpGet("seat")]
        public async Task<ActionResult> ListSeat(
            [FromQuery(Name ="date")]string dateString, [FromQuery(Name = "train_class")]string trainClass,
            [FromQuery(Name = "train_name")]string trainName, [FromQuery(Name = "car_number")]int carNumber,
            [FromQuery(Name = "from")]string fromName, [FromQuery(Name = "to")]string toName)
        {
            try
            {
                var date = new DateTimeOffset(DateTime.ParseExact(dateString, "yyyy-MM-dd", null), Utils.TokyoStandardTimeZone.BaseUtcOffset);

                if (!Utils.CheckAvailableDate(date))
                {
                    return new NotFoundObjectResult("予約可能期間外です");
                }

                var str = configuration.GetConnectionString("Isucon9");
                using var connection = new MySqlConnection(str);

                var query = "SELECT * FROM train_master WHERE date=@Date AND train_class=@trainClass AND train_name=@trainName";
                var train = await connection.QuerySingleOrDefaultAsync<TrainModel>(query,
                    new { date.Date, trainClass, trainName });
                if (train == null)
                {
                    return new NotFoundObjectResult("列車が存在しません");
                }

                query = "SELECT * FROM station_master WHERE name=@Name";
                var fromStation = await connection.QuerySingleAsync<StationModel>(query, new { Name = fromName });
                var toStation = await connection.QuerySingleAsync<StationModel>(query, new { Name = toName });

                var usableTrainClassList = Utils.GetUsableTrainClassList(fromStation, toStation);
                var usable = false;
                foreach (var v in usableTrainClassList)
                {
                    usable = v == train.TrainClass;
                }
                if (!usable)
                    throw new Exception("invalid train_class");

                query = "SELECT * FROM seat_master WHERE train_class=@trainClass AND car_number=@carNumber ORDER BY seat_row, seat_column";
                var seatList = await connection.QueryAsync<SeatModel>(query, new { trainClass, carNumber });

                var seatInformationList = new List<SeatInformationModel>();
                foreach (var seat in seatList)
                {
                    var s = new SeatInformationModel
                    {
                        Row = seat.SeatRow,
                        Column = seat.SeatColumn,
                        Class = seat.SeatClass,
                        IsSmokingSeat = seat.IsSmokingSeat,
                        IsOccupied = false
                    };

                    query = @"
SELECT s.* 
FROM seat_reservations s, reservations r 
WHERE 
	r.date=@Date AND r.train_class=@TrainClass AND r.train_name=@trainName AND car_number=@carNumber AND seat_row=@SeatRow AND seat_column=@SeatColumn";

                    var seatReservationList = await connection.QueryAsync<SeatReservationModel>(query,
                        new {date.Date,seat.TrainClass, trainName,seat.CarNumber,seat.SeatClass, seat.SeatColumn });
                    Console.WriteLine(seatReservationList);

                    foreach (var seatReservation in seatReservationList)
                    {
                        query = "SELECT * FROM reservations WHERE reservation_id=@ReservationId";
                        var reservation = await connection.QuerySingleAsync<ReservationModel>(query, new { seatReservation.ReservationId});

                        query = "SELECT * FROM station_master WHERE name=@Name";
                        var departureStation = await connection.QuerySingleAsync<StationModel>(query, new { Name = reservation.Departure });
                        var arrivalStation = await connection.QuerySingleAsync<StationModel>(query, new { Name = reservation.Arrival });

                        if (train.IsNobori)
                        {
                            // 上り
                            if (toStation.ID < arrivalStation.ID && fromStation.ID <= arrivalStation.ID)
                            {
                                // pass
                            }
                            else if (toStation.ID >= departureStation.ID && fromStation.ID > departureStation.ID) 
                            {
                                // pass
                            }
                            else
                            {
                                s.IsOccupied = true;
                            }
                        }
                        else
                        {
                            //  下り
                            if (fromStation.ID < departureStation.ID && toStation.ID <= departureStation.ID)
                            {
                                // pass
                            }
                            else if (fromStation.ID >= arrivalStation.ID && toStation.ID > arrivalStation.ID)
                            {
                                // pass
                            }
                            else
                            {
                                s.IsOccupied = true;
                            }
                        }
                        Console.WriteLine(s.IsOccupied);
                        seatInformationList.Add(s);
                    }
                }

                // 各号車の情報

                var simpleCarInformationList = new List<SimpleCarInformationModel>();
                query = "SELECT * FROM seat_master WHERE train_class=@trainClass AND car_number=@i ORDER BY seat_row, seat_column LIMIT 1";
                var i = 0;
                while (true)
                {
                    try
                    {
                        var seat = await connection.QuerySingleAsync<SeatModel>(query, new { trainClass, i });
                        simpleCarInformationList.Add(new SimpleCarInformationModel
                        {
                            CarNumber = i,
                            SeatClass = seat.SeatClass
                        });
                        i = i + 1;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                return new OkObjectResult(new CarInformationModel
                {
                    Date = date.ToString("yyyy/MM/dd"),
                    TrainClass = trainClass,
                    TrainName = trainName,
                    CarNumber = carNumber,
                    SeatInformationList = seatInformationList.ToArray(),
                    Cars = simpleCarInformationList.ToArray()
                });
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(e);
            }
        }

        [HttpPost("reserve")]
        public async Task<ActionResult> Reserve()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return null;
        }

        [HttpPost("reservation/commit")]
        public async Task<ActionResult> CommitReservation()
        {
            var api = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://localhost:5000";
            return null;
        }

        private async Task<int> FareCalc(DateTimeOffset date, int depStation, int destStation, string trainClass, string seatClass, MySqlConnection connection)
        {
            //
            // 料金計算メモ
            // 距離運賃(円) * 期間倍率(繁忙期なら2倍等) * 車両クラス倍率(急行・各停等) * 座席クラス倍率(プレミアム・指定席・自由席)
            //
            var query = "SELECT * FROM station_master WHERE id=@id";
            var fromStation = await connection.QuerySingleAsync<StationModel>(query, new { id = depStation });
            var toStation = await connection.QuerySingleAsync<StationModel>(query, new { id = destStation });

            Console.WriteLine($"distance {Math.Abs(toStation.Distance - fromStation.Distance)}");

            async Task<int> GetDistanceFare(double origToDestDistance)
            {
                var q = "SELECT distance,fare FROM distance_fare_master ORDER BY distance";
                var distanceFareList = await connection.QueryAsync<DistanceFareModel>(q);

                var lastDistance = 0d;
                var lastFare = 0;
                foreach (var distanceFare in distanceFareList)
                {
                    Console.WriteLine($"{origToDestDistance} {distanceFare.Distance} {distanceFare.Fare}");
                    if (lastDistance < origToDestDistance && origToDestDistance < distanceFare.Distance)
                    {
                        break;
                    }
                    lastDistance = distanceFare.Distance;
                    lastFare = distanceFare.Fare;
                }
                return lastFare;
            }
            var distFare = await GetDistanceFare(Math.Abs(toStation.Distance - fromStation.Distance));
            Console.WriteLine($"distFare {distFare}");

            // 期間・車両・座席クラス倍率
            query = "SELECT * FROM fare_master WHERE train_class=@trainClass AND seat_class=@seatClass ORDER BY start_date";
            var fareList = (await connection.QueryAsync<FareModel>(query, new { trainClass, seatClass })).ToArray();
            if (fareList.Length == 0)
            {
                throw new Exception("fare_master does not exists");
            }
            date = date.Date.ToUniversalTime();
            var selectedFare = fareList[0];
            foreach (var fare in fareList)
            {
                if (!(date < fare.StartDate))
                {
                    Console.WriteLine($"{fare.StartDate} {fare.FareMultiplier}");
                    selectedFare = fare;
                }
            }

            Console.WriteLine("%%%%%%%%%%%%%%%%%%%");
            return (int)(distFare * selectedFare.FareMultiplier);
        }
    }
}
