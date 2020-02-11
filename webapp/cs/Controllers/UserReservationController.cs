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
using System.Text.Json;
using System.Text;

namespace cs.Controllers
{
    [ApiController]
    [Route("api/user/reservations")]
    public class UserReservationController
    {
        private static readonly HttpClient paymentClient = new HttpClient();

        private readonly IConfiguration configuration;
        private readonly HttpContext httpContext;

        public UserReservationController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            this.configuration = configuration;
            httpContext = httpContextAccessor.HttpContext;
        }

        private async Task<ReservationResponseModel> ConstructReservation(MySqlConnection connection, ReservationModel reservation)
        {
            try
            {
                var departure = await connection.QuerySingleAsync<string>("SELECT departure FROM train_timetable_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName AND station=@Departure",
                    new { Date = reservation.Date.ToString("yyyy-MM-dd"), reservation.TrainClass, reservation.TrainName, reservation.Departure });

                var arrival = await connection.QuerySingleAsync<string>("SELECT departure FROM train_timetable_master WHERE date=@Date AND train_class=@TrainClass AND train_name=@TrainName AND station=@Arrival",
                    new { Date = reservation.Date.ToString("yyyy-MM-dd"), reservation.TrainClass, reservation.TrainName, reservation.Departure });

                var reservationResponse = new ReservationResponseModel
                {
                    ReservationId = reservation.ReservationId,
                    Date = reservation.Date.ToString("yyyy/MM/dd"),
                    Amount = reservation.Amount,
                    Adult = reservation.Adult,
                    Child = reservation.Child,
                    Departure = reservation.Departure,
                    Arrival = reservation.Arrival,
                    TrainClass = reservation.TrainClass,
                    TrainName = reservation.TrainName,
                    DepartureTime = departure,
                    ArrivalTime = arrival
                };

                var query = "SELECT * FROM seat_reservations WHERE reservation_id=@ReservationId";
                var reservationSeats = await connection.QueryAsync<SeatReservationModel>(query, new { reservation.ReservationId });
                // 1つの予約内で車両番号は全席同じ
                reservationResponse.CarNumber = reservationSeats.First().CarNumber;

                if (reservationSeats.First().CarNumber == 0)
                {
                    reservationResponse.SeatClass = "non-reserved";
                }
                else
                {
                    // 座席種別を取得
                    query = "SELECT * FROM seat_master WHERE train_class=@TrainClass AND car_number=@CarNumber AND seat_column=@SeatColumn AND seat_row=@SeatRow";
                    var seat = await connection.QuerySingleAsync<SeatModel>(query,
                        new {
                            reservation.TrainClass,
                            reservationResponse.CarNumber,
                            reservationSeats.First().SeatColumn,
                            reservationSeats.First().SeatRow
                        });
                    reservationResponse.SeatClass = seat.SeatClass;
                }

                foreach (var v in reservationSeats)
                {
                    v.ReservationId = 0;
                    v.CarNumber = 0;
                }
                reservationResponse.Seats = reservationSeats.ToArray();
                return reservationResponse;
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "makeReservationResponse()", e);
            }
        }

        [HttpGet]
        public async Task<ReservationResponseModel[]> List()
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);

            var user = await Utils.GetUser(httpContext, connection);
            IEnumerable<ReservationModel> reservationList;
            try
            {
                var query = "SELECT * FROM reservations WHERE user_id=@ID";
                reservationList = await connection.QueryAsync<ReservationModel>(query, new { user.ID });
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
            }

            var reservationResponseList = new List<ReservationResponseModel>();
            foreach (var r in reservationList)
            {
                reservationResponseList.Add(await ConstructReservation(connection, r));
            }
            return reservationResponseList.ToArray();
        }

        [HttpGet("/{id}")]
        public async Task<ReservationResponseModel> Get([FromRoute]long itemID)
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);

            var user = await Utils.GetUser(httpContext, connection);

            if (itemID < 0)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "incorrect item id");
            }

            ReservationModel reservation;
            try
            {
                var query = "SELECT * FROM reservations WHERE reservation_id=@itemID AND user_id=@ID";
                reservation = await connection.QuerySingleOrDefaultAsync<ReservationModel>(query,
                    new { itemID, user.ID });
                if (reservation == null)
                {
                    throw new HttpResponseException(StatusCodes.Status404NotFound, "Reservation not found");
                }
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, e);
            }

            return await ConstructReservation(connection, reservation);
        }

        [HttpPost("/{id}/cancel")]
        public async Task<MessageResponseModel> Cancel([FromRoute]long itemID)
        {
            var str = configuration.GetConnectionString("Isucon9");
            using var connection = new MySqlConnection(str);

            var user = await Utils.GetUser(httpContext, connection);

            if (itemID < 0)
            {
                throw new HttpResponseException(StatusCodes.Status400BadRequest, "incorrect item id");
            }

            using var tx = await connection.BeginTransactionAsync();

            ReservationModel reservation;
            try
            {
                var query = "SELECT * FROM reservations WHERE reservation_id=@itemID AND user_id=@ID";
                reservation = await connection.QuerySingleOrDefaultAsync<ReservationModel>(query,
                    new { itemID, user.ID });
                if (reservation == null)
                {
                    await tx.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status400BadRequest, "reservations naiyo");
                }
            }
            catch (Exception e)
            {
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, "予約情報の検索に失敗しました", e);
            }

            switch (reservation.Status)
            {
                case "rejected":
                    await tx.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "何らかの理由により予約はRejected状態です");
                case "done":
                    // 支払いをキャンセルする
                    var payInfo = new CancelPaymentInformationRequestModel { PaymentId = reservation.PaymentId };
                    var paymentApi = Environment.GetEnvironmentVariable("PAYMENT_API") ?? "http://payment:5000";

                    try
                    {
                        var res = await paymentClient.SendAsync(new HttpRequestMessage
                        {
                            Method = HttpMethod.Delete,
                            RequestUri = new Uri($"{payInfo}/payment/{reservation.PaymentId}"),
                            Content = new StringContent(JsonSerializer.Serialize(payInfo), Encoding.UTF8, @"application/json")
                        });

                        if ((int)res.StatusCode != StatusCodes.Status200OK)
                        {
                            await tx.RollbackAsync();
                            Console.WriteLine(res.StatusCode);
                            throw new HttpResponseException(StatusCodes.Status500InternalServerError, "決済のキャンセルに失敗しました");
                        }
                        using var contentStream = await res.Content.ReadAsStreamAsync();
                        var output = await JsonSerializer.DeserializeAsync<CancelPaymentInformationResponseModel>(contentStream);
                        Console.WriteLine(output);
                    }
                    catch (Exception e)
                    {
                        await tx.RollbackAsync();
                        throw new HttpResponseException(StatusCodes.Status500InternalServerError, "決済のキャンセルに失敗しました", e);
                    }

                    break;
                default:
                    // pass(requesting状態のものはpayment_id無いので叩かない)
                    break;
            }

            try
            {
                var query = "DELETE FROM reservations WHERE reservation_id=@itemID AND user_id=@ID";
                await connection.ExecuteAsync(query, new { itemID, user.ID });
                query = "DELETE FROM seat_reservations WHERE reservation_id=@itemID";
                var res = await connection.ExecuteAsync(query, new { itemID});
                if (res == 0)
                {
                    await tx.RollbackAsync();
                    throw new HttpResponseException(StatusCodes.Status500InternalServerError, "seat naiyo");
                }
            }
            catch (Exception e) when (!(e is HttpResponseException))
            {
                await tx.RollbackAsync();
                throw new HttpResponseException(StatusCodes.Status500InternalServerError, e);
            }
            await tx.CommitAsync();
            return new MessageResponseModel("cancell complete");
        }

    }
}
