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
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text.Json;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainController
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly IConfiguration configuration;
        private readonly HttpContext httpContext;


        public TrainController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            this.configuration = configuration;
            httpContext = httpContextAccessor.HttpContext;
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
        public async Task<TrainSearchResponseModel[]> Search(
            [FromQuery(Name = "use_at")]string useAt, [FromQuery]string from, [FromQuery]string to,
            [FromQuery(Name = "train_class")]string trainClass, [FromQuery]int adult, [FromQuery]int child)
        {
            Console.WriteLine("search...");
            DateTimeOffset date;
            try
            {
                var d = DateTimeOffset.Parse(useAt);
                date = TimeZoneInfo.ConvertTime(d, Utils.TokyoStandardTimeZone);
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
            }
            
            if (!Utils.CheckAvailableDate(date))
            {
                throw new HttpResponseException(StatusCodes.Status404NotFound, "予約可能期間外です");
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
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "fromStation: no rows");
                }
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, e);
            }
            try
            {
                toStation = await GetStation(to);
                if (toStation == null)
                {
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "toStation: no rows");
                }
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, e);
            }

            var isNobori = fromStation.Distance > toStation.Distance;

            var usableTrainClassList = Utils.GetUsableTrainClassList(fromStation, toStation);
            
            var query = $"SELECT * FROM train_master WHERE date=@Date AND train_class IN @usableTrainClassList AND is_nobori=@isNobori{(string.IsNullOrEmpty(trainClass) ? "" : " AND train_class=@trainClass")}";
            
            var trainList = await connection.QueryAsync<TrainModel>(query, new { Date=date.Date.ToString("yyyy-MM-dd"), usableTrainClassList, isNobori, trainClass });
            
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
                    Console.WriteLine("列車情報...");
                    try
                    {
                        // 所要時間
                        var departure = await connection.QuerySingleAsync<TimeSpan>(
                            "SELECT departure FROM train_timetable_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName AND station=@Name",
                            new { date.Date, train.TrainClass, train.TrainName, fromStation.Name });
                        
                        var departureDate = DateTimeOffset.Parse($"{date.ToString("yyyy/MM/dd")} {departure.ToString("c")} +09:00");
                        
                        if (!(date < departureDate))
                        {
                            // 乗りたい時刻より出発時刻が前なので除外
                            continue;
                        }
                        
                        var arrival = await connection.QuerySingleAsync<TimeSpan>(
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
                                {"non_reserved", "○"}
                            };

                            Console.WriteLine("料金計算...");
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
                                throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
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
                                DepartureTime = departure.ToString("c"),
                                ArrivalTime = arrival.ToString("c"),
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
                            throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new HttpResponseException(StatusCodes.Status500InternalServerError, e);
                    }
                }
            }
            return trainSearchResponseList.ToArray();
        }

        /// <summary>
        /// 指定した列車の座席列挙
        /// GET /train/seats?date=2020-03-01&train_class=のぞみ&train_name=96号&car_number=2&from=大阪&to=東京
        /// </summary>
        /// <returns></returns>
        [HttpGet("seat")]
        public async Task<CarInformationModel> ListSeat(
            [FromQuery(Name ="date")]string dateString, [FromQuery(Name = "train_class")]string trainClass,
            [FromQuery(Name = "train_name")]string trainName, [FromQuery(Name = "car_number")]int carNumber,
            [FromQuery(Name = "from")]string fromName, [FromQuery(Name = "to")]string toName)
        {
            try
            {
                var date = new DateTimeOffset(DateTime.ParseExact(dateString, "yyyy-MM-dd", null), Utils.TokyoStandardTimeZone.BaseUtcOffset);

                if (!Utils.CheckAvailableDate(date))
                {
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "予約可能期間外です");
                }

                var str = configuration.GetConnectionString("Isucon9");
                using var connection = new MySqlConnection(str);

                var query = "SELECT * FROM train_master WHERE date=@Date AND train_class=@trainClass AND train_name=@trainName";
                var train = await connection.QuerySingleOrDefaultAsync<TrainModel>(query,
                    new { date.Date, trainClass, trainName });
                if (train == null)
                {
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "列車が存在しません");
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
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "invalid train_class");

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

                return new CarInformationModel
                {
                    Date = date.ToString("yyyy/MM/dd"),
                    TrainClass = trainClass,
                    TrainName = trainName,
                    CarNumber = carNumber,
                    SeatInformationList = seatInformationList.ToArray(),
                    Cars = simpleCarInformationList.ToArray()
                };
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
            }
        }

        /// <summary>
        /// 列車の席予約API　支払いはまだ
        /// POST /api/train/reserve
        /// {
		///		"date": "2020-12-31T07:57:00+09:00",
		///		"train_name": "183",
		///		"train_class": "中間",
		///		"car_number": 7,
		///		"is_smoking_seat": false,
		///		"seat_class": "reserved",
		///		"departure": "東京",
		///		"arrival": "名古屋",
		///		"child": 2,
		///		"adult": 1,
		///		"column": "A",
		///		"seats": [
		///			{
		///			"row": 3,
		///			"column": "B"
        ///
        ///           },
		///				{
		///			"row": 4,
		///			"column": "C"
		///			}
		///		]
		///}
		///レスポンスで予約IDを返す
        ///reservationResponse(w http.ResponseWriter, errCode int, id int, ok bool, message string)
        /// </summary>
        /// <returns></returns>
        [HttpPost("reserve")]
        public async Task<TrainReservationResponseModel> Reserve([FromBody]TrainReservationRequestModel req)
        {
            // 乗車日の日付表記統一
            DateTimeOffset date;
            try
            {
                var d = DateTimeOffset.Parse(req.Date);
                date = TimeZoneInfo.ConvertTime(d, Utils.TokyoStandardTimeZone);
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, e);
            }

            if (!Utils.CheckAvailableDate(date))
            {
                throw new HttpResponseException(StatusCodes.Status404NotFound, "予約可能期間外です");
            }

            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            using var txn = await connection.BeginTransactionAsync();

            var query = "SELECT * FROM train_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName";
            TrainModel tmas;
            try
            {
                tmas = await connection.QuerySingleOrDefaultAsync<TrainModel>(query,
                    new { Date = date.ToString("yyyy-MM-dd"), req.TrainClass, req.TrainName });
                if (tmas == null)
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "列車データがみつかりません");
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "列車データの取得に失敗しました", e);
            }

            // 列車自体の駅IDを求める
            StationModel departureStation, arrivalStation;
            query = "SELECT * FROM station_master WHERE name=@Name";
            try
            {
                departureStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query, new { Name = tmas.StartStation });
                if (tmas == null)
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "リクエストされた列車の始発駅データがみつかりません");
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "リクエストされた列車の始発駅データの取得に失敗しました", e);
            }
            try
            {
                arrivalStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query, new { Name = tmas.LastStation });
                if (tmas == null)
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "リクエストされた列車の終着駅データがみつかりません");
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "リクエストされた列車の終着駅データの取得に失敗しました", e);
            }

            // リクエストされた乗車区間の駅IDを求める
            StationModel fromStation, toStation;
            try
            {
                fromStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query, new { Name = req.Departure });
                if (tmas == null)
                    throw new HttpResponseException(StatusCodes.Status404NotFound, $"乗車駅データがみつかりません {req.Departure}");
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "乗車駅データの取得に失敗しました", e);
            }
            try
            {
                toStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query, new { Name = req.Arrival });
                if (tmas == null)
                    throw new HttpResponseException(StatusCodes.Status404NotFound, $"降車駅データがみつかりません {req.Arrival}");
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "降車駅データの取得に失敗しました", e);
            }

            switch (req.TrainClass)
            {
                case "最速":
                    if (!fromStation.IsStopExpress || !toStation.IsStopExpress)
                    {
                        await txn.RollbackAsync();
                        throw new HttpResponseException(StatusCodes.Status400BadRequest, "最速の止まらない駅です");
                    }
                    break;
                case "中間":
                    if (!fromStation.IsStopSemiExpress || !toStation.IsStopSemiExpress)
                    {
                        await txn.RollbackAsync();
                        throw new HttpResponseException(StatusCodes.Status400BadRequest, "中間の止まらない駅です");
                    }
                    break;
                case "遅いやつ":
                    if (!fromStation.IsStopLocal || !toStation.IsStopLocal)
                    {
                        await txn.RollbackAsync();
                        throw new HttpResponseException(StatusCodes.Status400BadRequest, "遅いやつの止まらない駅です");
                    }
                    break;
                default:
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた列車クラスが不明です");
            }

            // 運行していない区間を予約していないかチェックする
            if (tmas.IsNobori)
            {
                if (fromStation.ID > departureStation.ID || toStation.ID > departureStation.ID)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた区間に列車が運行していない区間が含まれています");
                }
                if (arrivalStation.ID >= fromStation.ID || arrivalStation.ID > toStation.ID)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた区間に列車が運行していない区間が含まれています");
                }
            }
            else
            {
                if (fromStation.ID < departureStation.ID || toStation.ID < departureStation.ID)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた区間に列車が運行していない区間が含まれています");
                }
                if (arrivalStation.ID <= fromStation.ID || arrivalStation.ID < toStation.ID)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた区間に列車が運行していない区間が含まれています");
                }
            }

            /*
		        あいまい座席検索
		        seatsが空白の時に発動する
	        */
            switch (req.Seats.Count())
            {
                case 0:
                    if (req.SeatClass == "non-reserved")
                    {
                        break; // non-reservedはそもそもあいまい検索もせずダミーのRow/Columnで予約を確定させる。
                    }
                    //当該列車・号車中の空き座席検索
                    try
                    {
                        query = "SELECT * FROM train_master WHERE date=@Date AND train_class=@TrainCalss AND train_name=@TrainName";
                        var train = await connection.QueryFirstOrDefaultAsync<TrainModel>(query,
                            new { Date = date.ToString("yyyy-MM-dd"), req.TrainClass, req.TrainName });
                        if (train == null)
                            throw new Exception(); //panicの再現

                        var usableTrainClassList = Utils.GetUsableTrainClassList(fromStation, toStation);
                        var usable = false;
                        foreach (var v in usableTrainClassList)
                        {
                            if (v == train.TrainClass)
                            {
                                usable = true;
                            }
                        }

                        if (!usable)
                        {
                            await txn.RollbackAsync();
                            throw new HttpResponseException(StatusCodes.Status400BadRequest, "invalid train_class");
                        }

                        req.Seats = new List<RequestSeatModel>();
                        for (int carnum = 0; carnum < 16; carnum++)
                        {
                            query = "SELECT * FROM seat_master WHERE train_class=@TrainClass AND car_number=@carnum AND seat_class=@SeatClass AND is_smoking_seat=@IsSmokingSeat ORDER BY seat_row, seat_column";
                            var seatList = await connection.QueryAsync<SeatModel>(query,
                                new { req.TrainClass, carnum, req.SeatClass, req.IsSmokingSeat });

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

                                query = "SELECT s.* FROM seat_reservations s, reservations r WHERE r.date=@Date AND r.train_class=@TrainClass AND r.train_name=@TrainName AND car_number=@CarNumber AND seat_row=@SeatRow AND seat_column=@SeatColumn FOR UPDATE";
                                var seatReservationList = await connection.QueryAsync<SeatReservationModel>(query,
                                    new {Date= date.ToString("yyyy-MM-dd"), seat.TrainClass, req.TrainName, seat.CarNumber, seat.SeatRow, seat.SeatColumn });

                                foreach (var seatReservation in seatReservationList)
                                {
                                    query = "SELECT * FROM reservations WHERE reservation_id=@ReservationId FOR UPDATE";
                                    var reservation = await connection.QuerySingleAsync<ReservationModel>(query, new { seatReservation.ReservationId});

                                    query = "SELECT * FROM station_master WHERE name=@Name";
                                    var departureStation2 = await connection.QuerySingleAsync<StationModel>(query, new { Name = reservation.Departure});
                                    var arrivalStation2 = await connection.QuerySingleAsync<StationModel>(query, new { Name = reservation.Arrival});
                                    if (train.IsNobori)
                                    {
                                        if (toStation.ID < arrivalStation2.ID && fromStation.ID <= arrivalStation2.ID)
                                        {
                                            // pass
                                        }
                                        else if (toStation.ID >= departureStation2.ID && fromStation.ID > departureStation2.ID)
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
                                        if (fromStation.ID < departureStation2.ID && toStation.ID <= departureStation2.ID)
                                        {
                                            // pass
                                        }
                                        else if (fromStation.ID >= arrivalStation2.ID && toStation.ID > arrivalStation2.ID)
                                        {
                                            // pass
                                        }
                                        else
                                        {
                                            s.IsOccupied = true;
                                        }
                                    }
                                }
                                seatInformationList.Add(s);
                            }

                            // 曖昧予約席とその他の候補席を選出
                            var reserved = false; // あいまい指定席確保済フラグ
                            var vargue = true; // あいまい検索フラグ
                            var seatnum = (req.Adult + req.Child - 1);// 予約する座席の合計数。全体の人数からあいまい指定席分を引いておく
                            var vagueSeat = new RequestSeatModel();
                            if (req.Column == "") // A/B/C/D/Eを指定しなければ、空いている適当な指定席を取るあいまいモード
                            {
                                seatnum = (req.Adult + req.Child); // あいまい指定せず大人＋小人分の座席を取る
                                reserved = true; // dummy
                                vargue = false;  // dummy
                            }
                                                        
                            var candidateSeats = new List<RequestSeatModel>();
                            // シート分だけ回して予約できる席を検索
                            var i = 0;
                            foreach (var seat in seatInformationList)
                            {
                                // あいまい席があいてる
                                if (seat.Column == req.Column && !seat.IsOccupied && !reserved && vargue)
                                {
                                    vagueSeat.Row = seat.Row;
                                    vagueSeat.Column = seat.Column;
                                    reserved = true;
                                }
                                // 単に席があいてる
                                else if (!seat.IsOccupied && i < seatnum)
                                {
                                    candidateSeats.Add(new RequestSeatModel
                                    {
                                        Row = seat.Row,
                                        Column = seat.Column
                                    });
                                    i++;
                                }
                            }

                            // あいまい席が見つかり、予約できそうだった
                            if (vargue && reserved)
                            {
                                // あいまい予約席を追加
                                req.Seats.Add(vagueSeat);
                            }

                            // 候補席があった
                            if (i > 0)
                            {
                                // 予約候補席追加
                                req.Seats.AddRange(candidateSeats);
                            }

                            if (req.Seats.Count() < req.Adult + req.Child)
                            {
                                // リクエストに対して席数が足りてない
                                // 次の号車にうつしたい
                                Console.WriteLine("-----------------");
                                Console.WriteLine($"現在検索中の車両: {carnum}号車, リクエスト座席数: {req.Adult + req.Child}, 予約できそうな座席数: {req.Seats.Count()}, 不足数: {req.Adult + req.Child-req.Seats.Count()}");
                                Console.WriteLine("リクエストに対して座席数が不足しているため、次の車両を検索します。");
                                req.Seats = new List<RequestSeatModel>();
                                if (carnum == 16)
                                {
                                    Console.WriteLine("この新幹線にまとめて予約できる席数がなかったから検索をやめるよ");
                                    req.Seats = new List<RequestSeatModel>();
                                    break;
                                }
                            }
                            Console.WriteLine($"空き実績: {carnum}号車 シート:{string.Join(",", req.Seats)} 席数:{req.Seats.Count()}");
                            if (req.Seats.Count() >= req.Adult + req.Child)
                            {
                                Console.WriteLine("予約情報に追加したよ");
                                req.Seats.Take(req.Adult + req.Child).ToList();
                                req.CarNumber = carnum;
                                break;
                            }
                        }
                        if (req.Seats.Count() == 0)
                        {
                            await txn.RollbackAsync();
                            throw new HttpResponseException(StatusCodes.Status404NotFound, "あいまい座席予約ができませんでした。指定した席、もしくは1車両内に希望の席数をご用意できませんでした。");
                        }
                    }
                    catch (Exception e) when (!(e is HttpResponseException))
                    {
                        await txn.RollbackAsync();
                        throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
                    }
                    break;
                default:
                    // 座席情報のValidate
                    var seatLisT = new List<SeatModel>();
                    foreach (var z in req.Seats)
                    {
                        Console.WriteLine($"xxxx {z}");
                        query = "SELECT * FROM seat_master WHERE train_class=@TrainClass AND car_number=CarNumber AND seat_column=@Column AND seat_row=@Row AND seat_class=@SeatClass";
                        var seat = await connection.QuerySingleOrDefaultAsync<SeatModel>(query,
                            new { req.TrainClass, req.CarNumber, z.Column, z.Row, req.SeatClass,});
                        if (seat == null)
                        {
                            await txn.RollbackAsync();
                            throw new HttpResponseException(StatusCodes.Status404NotFound, "リクエストされた座席情報は存在しません。号車・喫煙席・座席クラスなど組み合わせを見直してください");
                        }
                    }
                    break;
            }

            // 当該列車・列車名の予約一覧取得
            query = "SELECT * FROM reservations WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName FOR UPDATE";
            IEnumerable<ReservationModel> reservartions;
            try
            {
                reservartions = await connection.QueryAsync<ReservationModel>(query,
                    new { Date = date.ToString("yyyy-MM-dd"), req.TrainClass, req.TrainName });
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "列車予約情報の取得に失敗しました", e);
            }

            foreach (var reservation in reservartions)
            {
                if (req.SeatClass == "non-reserved")
                {
                    break;
                }
                // train_masterから列車情報を取得(上り・下りが分かる)
                query = "SELECT * FROM train_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName";
                try
                {
                    tmas = await connection.QuerySingleOrDefaultAsync<TrainModel>(query,
                            new { Date = date.ToString("yyyy-MM-dd"), req.TrainClass, req.TrainName });
                    if (tmas == null)
                    {
                        throw new HttpResponseException(StatusCodes.Status404NotFound, "列車データがみつかりません");
                    }
                }
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "列車データの取得に失敗しました", e);
                }

                // 予約情報の乗車区間の駅IDを求める
                StationModel reservedfromStation, reservedtoStation;
                query = "SELECT * FROM station_master WHERE name=@Name";
                try
                {
                    reservedfromStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query,
                            new { Name = reservation.Departure });
                    if (tmas == null)
                    {
                        throw new HttpResponseException(StatusCodes.Status404NotFound, "予約情報に記載された列車の乗車駅データがみつかりません");
                    }
                }
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "予約情報に記載された列車の乗車駅データの取得に失敗しました", e);
                }
                try
                {
                    reservedtoStation = await connection.QuerySingleOrDefaultAsync<StationModel>(query,
                            new { Name = reservation.Arrival });
                    if (tmas == null)
                    {
                        throw new HttpResponseException(StatusCodes.Status404NotFound, "予約情報に記載された列車の降車駅データがみつかりません");
                    }
                }
                catch (Exception e)
                {
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "予約情報に記載された列車の降車駅データの取得に失敗しました", e);
                }

                // 予約の区間重複判定
                var secdup = false;
                if (tmas.IsNobori)
                {
                    // 上り
                    if (toStation.ID < reservedtoStation.ID && fromStation.ID <= reservedtoStation.ID)
                    {
                        // pass
                    }
                    else if (toStation.ID >= reservedfromStation.ID && fromStation.ID > reservedfromStation.ID)
                    {
                        // pass
                    }
                    else
                    {
                        secdup = true;
                    }
                }
                else
                {
                    // 下り
                    if (fromStation.ID < reservedfromStation.ID && toStation.ID <= reservedfromStation.ID)
                    {
                        // pass
                    }
                    else if (fromStation.ID >= reservedtoStation.ID && toStation.ID > reservedtoStation.ID)
                    {
                        // pass
                    }
                    else
                    {
                        secdup = true;
                    }
                }

                if (secdup)
                {
                    // 区間重複の場合は更に座席の重複をチェックする
                    IEnumerable<SeatReservationModel> seatReservations;
                    query = "SELECT * FROM seat_reservations WHERE reservation_id=@ReservationId FOR UPDATE";
                    try
                    {
                        seatReservations = await connection.QueryAsync<SeatReservationModel>(query,
                            new { reservation.ReservationId });
                    }
                    catch (Exception e)
                    {
                        throw new HttpResponseException(StatusCodes.Status500InternalServerError, "座席予約情報の取得に失敗しました", e);
                    }

                    foreach (var v in seatReservations)
                        foreach (var seat in req.Seats)
                        {
                            if (v.CarNumber == req.CarNumber && v.SeatRow == seat.Row && v.SeatColumn == seat.Column)
                            {
                                await txn.RollbackAsync();
                                throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストに既に予約された席が含まれています");
                            }
                        }                    
                }
            }
            // 3段階の予約前チェック終わり

            // 自由席は強制的にSeats情報をダミーにする（自由席なのに席指定予約は不可）
            if (req.SeatClass == "non-reserved")
            {
                req.Seats = new List<RequestSeatModel>();
                req.CarNumber = 0;
                for (int num = 0; num < req.Adult + req.Child; num++)
                {
                    req.Seats.Add(new RequestSeatModel{ Row = 0, Column = ""});
                }
            }

            // 運賃計算
            async Task<int> FareCalcBySeat(string seatClass)
            {
                try
                {
                    return await FareCalc(date, fromStation.ID, toStation.ID, req.TrainClass, seatClass, connection);
                }
                catch (Exception e)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, $"fareCalc {e.Message}", e);
                }
            }
            async Task<int> Throw()
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "リクエストされた座席クラスが不明です");
            }
#pragma warning disable CS8509 // switch 式が入力の種類のうちすべての可能な入力を処理していません (すべてを網羅していません)。
            var fare = req.SeatClass switch
#pragma warning restore CS8509 // switch 式が入力の種類のうちすべての可能な入力を処理していません (すべてを網羅していません)。
            {
                "premium" => await FareCalcBySeat("premium"),
                "reserved" => await FareCalcBySeat("reserved"),
                "non-reserved" => await FareCalcBySeat("non-reserved"),
                null => await Throw(),
            };
            var sumFare = (req.Adult * fare) + (req.Child * fare) / 2;
            Console.WriteLine("SUMFARE");

            // userID取得。ログインしてないと怒られる。
            var user = await Utils.GetUser(httpContext, connection, txn);

            //予約ID発行と予約情報登録
            query = @"
INSERT INTO `reservations` 
  (`user_id`, `date`, `train_class`, `train_name`, `departure`, `arrival`, `status`, `payment_id`, `adult`, `child`, `amount`) 
VALUES 
  (@ID, @Date, @TrainClass, @TrainName, @Departure, @Arrival, @Status, @PaymentId, @Adult, @Child, @sumFare);

SELECT LAST_INSERT_ID();
";
            long id;
            try
            {
                id = await connection.ExecuteScalarAsync<uint>(query,
                    new
                    {
                        user.ID,
                        Date = date.ToString("yyyy-MM-dd"),
                        req.TrainClass,
                        req.TrainName,
                        req.Departure,
                        req.Arrival,
                        Status = "requesting",
                        PaymentId = "a",
                        req.Adult,
                        req.Child,
                        sumFare
                    });
            }
            catch (Exception e)
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "予約の保存に失敗しました。", e);
            }

            //席の予約情報登録
            //reservationsレコード1に対してseat_reservationstが1以上登録される
            query = @"
INSERT INTO `seat_reservations` (`reservation_id`, `car_number`, `seat_row`, `seat_column`) 
VALUES (@id, @CarNumber, @Row, @Column)";
            try
            {
                foreach (var v in req.Seats)
                {
                    await connection.ExecuteAsync(query, new { id, req.CarNumber, v.Row, v.Column });
                }
            }
            catch (Exception e)
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "座席予約の登録に失敗しました。", e);
            }
            await txn.CommitAsync();
            return new TrainReservationResponseModel
            {
                ReservationId = id,
                Amount = sumFare,
                IsOk = true
            };
        }

        /// <summary>
        /// 支払い及び予約確定API
        /// POST /api/train/reservation/commit
        /// {
        /// 	"card_token": "161b2f8f-791b-4798-42a5-ca95339b852b",
        /// 	"reservation_id": "1"
        /// }
        /// 前段でフロントがクレカ非保持化対応用のpayment-APIを叩き、card_tokenを手に入れている必要がある
        /// 
        /// </summary>
        /// <returns>レスポンスは成功か否かのみ返す</returns>
        [HttpPost("reservation/commit")]
        public async Task<ReservationPaymentResponseModel> CommitReservation(
            [FromBody] ReservationPaymentRequestModel req)
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);
            using var txn = await connection.BeginTransactionAsync();
            
            // 予約IDで検索
            ReservationModel reservation;
            try
            {
                var query = "SELECT * FROM reservations WHERE reservation_id=?";
                reservation = await connection.QuerySingleOrDefaultAsync<ReservationModel>(query, new { req.ReservationId });
                if (reservation == null)
                {
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "予約情報がみつかりません");
                }
            }
            catch (Exception e)
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status404NotFound, "予約情報の取得に失敗しました", e);
            }

            // 支払い前のユーザチェック。本人以外のユーザの予約を支払ったりキャンセルできてはいけない。
            var user = await Utils.GetUser(httpContext, connection, txn);
            if (reservation.UserId != user.ID)
            {
                throw new HttpResponseException(StatusCodes.Status403Forbidden, "他のユーザIDの支払いはできません");
            }

            switch (reservation.Status)
            {
                case "done":
                    await txn.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status403Forbidden, "既に支払いが完了している予約IDです");
                default:
                    break;
            }

            // 決済する
            PaymentResponseModel output;
            try
            {
                var payInfo = new PaymentInformationRequestModel
                {
                    CardToken = req.CardToken,
                    ReservationId = req.ReservationId,
                    Amount = reservation.Amount
                };
                var paymentApi = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://payment:5000";
                var res = await httpClient.PostAsync($"{paymentApi}/payment", new StringContent(JsonSerializer.Serialize(payInfo), Encoding.UTF8, @"application/json"));
                if ((int)res.StatusCode != StatusCodes.Status200OK)
                {
                    await txn.RollbackAsync();
                    Console.WriteLine(res.StatusCode);
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "決済に失敗しました。カードトークンや支払いIDが間違っている可能性があります");
                }
                using var contentStream = await res.Content.ReadAsStreamAsync();
                output = await JsonSerializer.DeserializeAsync<PaymentResponseModel>(contentStream);
            }
            catch (Exception e)
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "HTTP POSTに失敗しました", e);
            }

            // 予約情報の更新
            try
            {
                var query = "UPDATE reservations SET status=@Status, payment_id=@PaymentId WHERE reservation_id=@ReservationId";
                await connection.ExecuteAsync(query, new { Status="done", output.PaymentId, req.ReservationId});
            }
            catch (Exception e)
            {
                await txn.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "予約情報の更新に失敗しました", e);
            }

            await txn.CommitAsync();

            return new ReservationPaymentResponseModel { IsOk = true };
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
