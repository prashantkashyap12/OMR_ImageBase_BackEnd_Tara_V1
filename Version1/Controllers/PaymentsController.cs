using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using cashfree_pg.Model;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json.Linq;
using Dapper;
using Org.BouncyCastle.Ocsp;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        public PaymentsController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderModelIns request)
        {
            dynamic res;
            try
            {
                var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                if (string.IsNullOrWhiteSpace(getToken))
                {
                    return Unauthorized(new { message = "No token provided" });
                }
                var handler = new JwtSecurityTokenHandler();
                var TokenDecription = handler.ReadJwtToken(getToken);
                var Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                var orderId = $"OMR{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                // Payload for cashfree gatway
                var cashfreeRequest = new
                {
                    order_id = orderId,
                    order_amount = request.Order_Amount,
                    order_currency = "INR",
                    order_meta = new
                    {
                        return_url = $"http://localhost:3000/payment-status?order_id={orderId}"
                    },
                    customer_details = new
                    {
                        customer_id = Empid,
                        customer_name = request.Customer_Details.Customer_Name,
                        customer_email = request.Customer_Details.Customer_Email,
                        customer_phone = request.Customer_Details.Customer_Phone
                    }
                };

                // Cashfree Api calling - request "cashfreeRequest" responce as json.
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_configuration["Cashfree:Environment"]}/orders");
                httpRequest.Headers.Add( "x-client-id",_configuration["Cashfree:AppId"]);
                httpRequest.Headers.Add("x-client-secret", _configuration["Cashfree:ApiSecret"]);
                httpRequest.Headers.Add("x-api-version", _configuration["Cashfree:ApiVersion"]);
                httpRequest.Content = new StringContent( JsonSerializer.Serialize(cashfreeRequest), Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(httpRequest);
                var result = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(result);
                if (response.IsSuccessStatusCode)
                {
                    using (var _conn = new SqlConnection(_configuration.GetConnectionString("dbc")))
                    {
                        _conn.Open();
                        DateTime currentTime = DateTime.Now;
                        var order_expiry_time1 = currentTime.AddMinutes(15);

                        var querry = $"insert into PaymentOrders (OrderId, PaymentSessionId, EmpId, PackageId, Amount, PaymentStatus, TransactionId, PaymentMethod, CreatedDate, UpdatedDate) " +
                            $"values ('{orderId}', '{json["payment_session_id"]?.ToString()}', {Empid}, {request.PackageId}, {request.Order_Amount}, 'Pending', 'Transaction','PaymentMethod', '{currentTime}', '{order_expiry_time1}')";
                        var resp = await _conn.ExecuteAsync(querry);
                        Console.WriteLine(resp);
                        _conn.Close();
                    }
                    res = new
                    {
                        status = true,
                        result2 = result,  // order detasils > meta url <>

                        Data = new
                        {
                            order_id = json["order_id"]?.ToString(),
                            payment_session_id = json["payment_session_id"]?.ToString(),
                            order_amount = json["order_amount"]?.ToObject<decimal>(),
                            order_currency = json["order_currency"]?.ToString(),
                        }
                    };
                }
                else
                {
                    res = new
                    {
                        status = false,
                        data = response
                    };
                }
                return Ok(res);
            }
            catch (Exception ex)
            {
                res = new {
                    status = false,
                    message = ex.Message
                };
                return Ok(res);
            }
        }
       
        [HttpGet("verify/{orderId}")]
        public async Task<IActionResult> Verify(string orderId)
        {
            dynamic res;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add( "x-client-id", _configuration["Cashfree:AppId"]);
                client.DefaultRequestHeaders.Add( "x-client-secret", _configuration["Cashfree:ApiSecret"]);
                client.DefaultRequestHeaders.Add( "x-api-version", _configuration["Cashfree:ApiVersion"]);
                var response = await client.GetAsync( $"{_configuration["Cashfree:Environment"]}/orders/{orderId}");
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        status = false,
                        Message = content
                    });
                }
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
                var orderStatus = (string)data.order_status;
                string paymode = data?.payment_method?.payment_group?.ToString() ?? "";
                string returnUrl = data?.order_meta?.return_url?.ToString() ?? $"http://localhost:3000/payment-status?order_id={orderId}";
                var walletMsg="";
                bool state = false;

                // Wallet update
                using (var _conn = new SqlConnection(_configuration.GetConnectionString("dbc")))
                {
                    await _conn.OpenAsync();
                    var payment = await _conn.QueryFirstOrDefaultAsync("SELECT * FROM PaymentOrders WHERE OrderId=@OrderId", new { OrderId = orderId });
                    DateTime updatedDate = Convert.ToDateTime(payment.UpdatedDate);
                    if (payment == null)
                    {
                        walletMsg = "Order not found";
                    }
                    else if (updatedDate < DateTime.Now)
                    {
                        return Ok(new
                        {
                            status = false,
                            Message = "Payment link expired",
                            ReturnLink = returnUrl
                        });
                    }
                    else if (payment.PaymentStatus != "PAID" && orderStatus == "PAID")
                    {
                        // Wallet credit karo
                        var resp = await _conn.QueryFirstOrDefaultAsync($"UPDATE PaymentOrders SET PaymentStatus='{(string)data.order_status}', UpdatedDate = '{DateTime.Now.ToString()}', PaymentMethod='{paymode}' OUTPUT INSERTED.* where OrderId = '{orderId}'");
                        var getPakcageVal = await _conn.QueryFirstOrDefaultAsync($"select * from Packages where PackId = {resp.PackageId}");
                        var Total1 = await _conn.QueryFirstAsync($"select * from Wallet where Uid = '{resp.EmpId}'");
                        Console.WriteLine();
                        var Total = Convert.ToInt32(getPakcageVal.creditLimit) + Convert.ToInt32(Total1.CreditLimit);
                        var UpdateWallet = $"Update Wallet SET CreditLimit = {Total}, TimeDate = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', recognition_credits_total = {getPakcageVal.creditLimit} where Uid = '{resp.EmpId}'";
                        var UpdateRes = await _conn.ExecuteAsync(UpdateWallet);
                        walletMsg = "Payment done and wallet updated";
                        state = true;
                    }
                    else if (orderStatus == "PAID")
                    {
                        walletMsg = "Payment already processed";
                        state = true;
                    }
                    else if (orderStatus == "ACTIVE")
                    {
                        walletMsg = "Payment pending";
                        state = false;
                    }
                    else
                    {
                        walletMsg = $"Payment status : {orderStatus}";
                        state = false;
                    }
                    _conn.Close();
                }
                res = new
                {
                    status = state,
                    Data = new
                    {
                        order_id = (string)data.order_id,
                        order_status = (string)data.order_status,
                        order_amount = (decimal)data.order_amount
                    },
                    Message = walletMsg, 
                    PaymentMode = paymode,
                    ReturnLink = returnUrl
                };
            }
            catch (Exception ex)
            {
                res = new
                {
                    status = false,
                    Message = ex.Message
                };
            }
            return Ok(res);
        }

        //[HttpPost("create-payment-link")]
        //public async Task<IActionResult> CreatePaymentLink([FromBody] CreateOrderModelIns request)
        //{
        //    dynamic res;

        //    try
        //    {
        //        var getToken = Request.Headers["Authorization"]
        //            .FirstOrDefault()?.Replace("Bearer ", "").Trim();

        //        if (string.IsNullOrWhiteSpace(getToken))
        //        {
        //            return Unauthorized(new { message = "No token provided" });
        //        }

        //        var handler = new JwtSecurityTokenHandler();
        //        var tokenDescription = handler.ReadJwtToken(getToken);

        //        var empId = tokenDescription.Claims
        //            .FirstOrDefault(c => c.Type == "nameid")?.Value;

        //        var orderId = $"OMR{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        //        var linkRequest = new
        //        {
        //            customer_details = new
        //            {
        //                customer_name = request.Customer_Details.Customer_Name,
        //                customer_email = request.Customer_Details.Customer_Email,
        //                customer_phone = request.Customer_Details.Customer_Phone
        //            },
        //            link_amount = request.Order_Amount,
        //            link_currency = "INR",
        //            link_purpose = $"Package Purchase - {request.PackageId}"
        //        };

        //        var httpRequest = new HttpRequestMessage(
        //            HttpMethod.Post,
        //            $"{_configuration["Cashfree:Environment"]}/links"
        //        );

        //        httpRequest.Headers.Add("x-client-id", _configuration["Cashfree:AppId"]);
        //        httpRequest.Headers.Add("x-client-secret", _configuration["Cashfree:ApiSecret"]);
        //        httpRequest.Headers.Add("x-api-version", _configuration["Cashfree:ApiVersion"]);

        //        httpRequest.Content = new StringContent(
        //            JsonSerializer.Serialize(linkRequest),
        //            Encoding.UTF8,
        //            "application/json"
        //        );

        //        var response = await _httpClient.SendAsync(httpRequest);
        //        var result = await response.Content.ReadAsStringAsync();
        //        if (!response.IsSuccessStatusCode)
        //        {
        //            return BadRequest(new
        //            {
        //                status = false,
        //                message = result
        //            });
        //        }
        //        var json = JObject.Parse(result);
        //        // Optional DB Save
        //        using (var conn = new SqlConnection(_configuration.GetConnectionString("dbc")))
        //        {
        //            await conn.OpenAsync();
        //            string linkId = json["link_id"]?.ToString() ?? "";
        //            string query = @"
        //        INSERT INTO PaymentOrders
        //        (
        //            OrderId,
        //            PaymentSessionId,
        //            EmpId,
        //            PackageId,
        //            Amount,
        //            PaymentStatus,
        //            TransactionId,
        //            PaymentMethod,
        //            CreatedDate,
        //            UpdatedDate
        //        )
        //        VALUES
        //        (
        //            @OrderId,
        //            @PaymentSessionId,
        //            @EmpId,
        //            @PackageId,
        //            @Amount,
        //            'Pending',
        //            '',
        //            '',
        //            @CreatedDate,
        //            @UpdatedDate
        //        )";
        //            await conn.ExecuteAsync(query, new
        //            {
        //                OrderId = linkId,
        //                PaymentSessionId = linkId,
        //                EmpId = empId,
        //                PackageId = request.PackageId,
        //                Amount = request.Order_Amount,
        //                CreatedDate = DateTime.Now,
        //                UpdatedDate = DateTime.Now
        //            });
        //        }
        //        res = new
        //        {
        //            status = true,
        //            Data = new
        //            {
        //                link_id = json["link_id"]?.ToString(),
        //                payment_url = json["link_url"]?.ToString(),
        //                link_status = json["link_status"]?.ToString()
        //            }
        //        };
        //        return Ok(res);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = ex.Message
        //        });
        //    }
        //}


    }
}

    public class CreateOrderModelIns
    {
        public decimal Order_Amount { get; set; }
        public int PackageId { get; set; }
        public CustomerDetails Customer_Details { get; set; }
    }
    public class CustomerDetails
    {
        public string Customer_Name { get; set; }
        public string Customer_Email { get; set; }
        public string Customer_Phone { get; set; }
    }


