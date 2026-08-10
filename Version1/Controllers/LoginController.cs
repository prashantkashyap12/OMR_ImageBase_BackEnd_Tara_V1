using System.Drawing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using Sentry;
using SQCScanner.Services;
using Syncfusion.EJ2.Notifications;
using Version1.Data;
using YourProject.Models;
using ZXing.Aztec.Internal;
using static OpenCvSharp.Stitcher;

namespace SQCScanner.Controllers
{
    [Route("api/auth/qr")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _Configration;
        private readonly string _connectionString;
        private readonly JwtAuth _jwtTokenGen;

        public LoginController(ApplicationDbContext context, IConfiguration configration, JwtAuth jwtTokenGen)
        {
            _context = context;
            _Configration = configration;
            _connectionString = configration.GetConnectionString("dbc");
            _jwtTokenGen = jwtTokenGen;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateQrSession(string? sessionId)
        {
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var session = new QrLoginSession
            {
                SessionId = Guid.NewGuid(),
                Token = GenerateSecureToken(),
                Status = "Pending",
                UserId = 0,
                CreatedAt = istTime,
                ExpiresAt = istTime.AddMinutes(2)
            };
            dynamic res;
            try
            {
                // c8d74153-b9fd-4383-9dcb-52b3bebafa31
                using (var _conn = new SqlConnection(_connectionString))
                {
                    var responseEntry = _conn.QueryFirstOrDefault<QrLoginSession>("SELECT * FROM QrLoginSessions WHERE SessionId = @sSessionId", new { sSessionId = sessionId });
                    // why we need to recheck 
                    var token = "";
                    if (responseEntry?.Status != null)
                    {
                        token = responseEntry.Token;
                        var isVerify = responseEntry.UserId;
                        if (isVerify == 0)
                        {
                            var qurry = $"delete QrLoginSessions where SessionId=@session";
                            var respose = await _conn.ExecuteAsync(qurry, new { session = sessionId });
                            var querry = $"insert into QrLoginSessions (SessionId, Status, UserId, CreatedAt, ExpiresAt, Token) values (@sessionId, @status, @useId, @createdAt, @expiresAt, @token)";
                            var responce = await _conn.ExecuteAsync(querry,
                                new
                                {
                                    sessionId = session.SessionId,
                                    status = session.Status,
                                    useId = session.UserId,
                                    createdAt = session.CreatedAt,
                                    expiresAt = session.ExpiresAt,
                                    token = session.Token
                                }
                            );
                            res = new
                            {
                                status = true,
                                message = "QR Session Created",
                                data = new QrCreateResponse
                                {
                                    SessionId = session.SessionId,
                                    ExpiresAt = session.ExpiresAt
                                }
                            };
                        }
                        else
                        {
                            res = new
                            {
                                status = true,
                                message = "User verified",

                                data = new QrCreateResponse
                                {
                                    Token = token
                                }
                            };

                        }
                    }
                    else
                    {
                        var querry = $"insert into QrLoginSessions (SessionId, Status, UserId, CreatedAt, ExpiresAt, Token) values (@sessionId, @status, @useId, @createdAt, @expiresAt, @token)";
                        var responce = await _conn.ExecuteAsync(querry,
                            new
                            {
                                sessionId = session.SessionId,
                                status = session.Status,
                                useId = session.UserId,
                                createdAt = session.CreatedAt,
                                expiresAt = session.ExpiresAt,
                                token = session.Token
                            }
                        );
                        res = new
                        {
                            status = true,
                            message = "QR Session Created",
                            data = new QrCreateResponse
                            {
                                SessionId = session.SessionId,
                                ExpiresAt = session.ExpiresAt
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    status = false,
                    message = ex.Message
                };
            }
            return Ok(res);
        }
        private string GenerateSecureToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes);
        }
        public class QrCreateResponse
        {
            public Guid SessionId { get; set; }
            public string Token { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }



        [Authorize]
        [HttpPost("scan")]
        public async Task<IActionResult> ScanQr([FromBody] ScanQrRequest model)
        {
            dynamic res;
            try
            {
                // JWT se EmpId nikalo
                var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                if (string.IsNullOrWhiteSpace(getToken))
                {
                    return Unauthorized(new { message = "No token provided" });
                }
                var handler = new JwtSecurityTokenHandler();
                var TokenDecription = handler.ReadJwtToken(getToken);
                var empId = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                if (string.IsNullOrEmpty(empId))
                {
                    return Unauthorized(new
                    {
                        status = false,
                        message = "Invalid token."
                    });
                }

                // User Find
                var user = await _context.empModels.FirstOrDefaultAsync(x => x.EmpId == empId);
                if (user == null)
                {
                    return NotFound(new
                    {
                        status = false,
                        message = "User not found."
                    });
                }

                // QR Session Find and verify via DAPPER -- FIX it
                using (var _conn = new SqlConnection(_connectionString))
                {
                    var resplist = _conn.QueryFirstOrDefault<QrLoginSession>($"select * from QrLoginSessions where SessionId = @session", new { session = model.SessionId });
                    Console.WriteLine(resplist);
                    if (resplist == null)
                    {
                        return NotFound(new
                        {
                            status = false,
                            message = "QR Session not found."
                        });
                    }

                    // Expired?
                    TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                    if (resplist.ExpiresAt <= istTime)
                    {
                        return BadRequest(new
                        {
                            status = false,
                            message = "QR Code expired."
                        });
                    }

                    //// Already Used?
                    if (resplist.Status != "Pending")
                    {
                        return BadRequest(new
                        {
                            status = false,
                            message = "QR Code already used."
                        });
                    }

                    // Web JWT Generate
                    var webToken = _jwtTokenGen.GenerateJwtToken(user);

                    // Session Update
                    var responce = await _conn.ExecuteAsync("update QrLoginSessions set Status = 'Active', UserId = @userId, Token = @getTokenn where SessionId = @session",
                        new
                        {
                            userId = empId,
                            getTokenn = webToken,
                            session = model.SessionId
                        }
                    );

                    if (responce == 1)
                    {
                        // Clean old data form SQL
                        string clean = $"delete QrLoginSessions WHERE [Status] = 'Active' AND [UserId] = '{empId}' AND [SessionId] NOT IN " +
                             $"( SELECT TOP (3) [SessionId] FROM QrLoginSessions WHERE [CreatedAt] >= {resplist.ExpiresAt.AddMinutes(-10)} " +
                             $"AND [Status] = 'Active' AND [UserId] = '{empId}' ORDER BY [CreatedAt] DESC, [SessionId] DESC );";
                        Console.WriteLine(clean);
                        var responcView = _conn.Execute(clean);
                    }

                    res = new
                    {
                        status = true,
                        message = "Verify Successfully"
                    };
                }
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
        public class ScanQrRequest
        {
            public Guid SessionId { get; set; }
        }

    }
}
