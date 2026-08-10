using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using Syncfusion.EJ2.Notifications;
using TesseractOCR.Renderers;
using Version1.Data;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SQCScanner.Controllers
{

    [Authorize]
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    [ApiController]
    public class showRecord : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        private readonly IHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public showRecord(IConfiguration configuration, IHostEnvironment env, ApplicationDbContext context)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
            _env = env;
            _context = context;
        }

        [HttpGet]
        [Route("getRec")]
        public async Task <IActionResult> Index(int Id, string direction, int tempId)
        {
            dynamic res = null;
            string query;
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    if (direction == "up")
                    {
                        query = $"SELECT TOP 200 * FROM Template_{tempId} where id <= {Id} ORDER BY Id";
                        var resw = await connection.QueryAsync(query);
                        res = new
                        {
                            state = true,
                            message = "Record Found",
                            data = resw
                        };
                    }
                    else if (direction == "down")
                    {
                        query = $"SELECT TOP 200 * FROM Template_{tempId} where id >= {Id} ORDER BY Id";
                        var resw = await connection.QueryAsync(query);
                        res = new
                        {
                            state = true,
                            message = "Record Found",
                            data = resw
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = "Error Occured",
                    error = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet]
        [Route("LastRec")]
        public async Task<IActionResult> mainVal(string TempId, string token)
        {
            dynamic res;
            string query = "";
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;
                using (var _conn = new SqlConnection(_connectionString))
                {
                    var tempTable = "Template_" + TempId;
                    var query2 = await _conn.ExecuteScalarAsync<int>($@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tempTable}'");
                    if (query2 > 0)
                    {
                        query = @$"select TOP 100 * from {tempTable} where UserName ='{userName}' ORDER BY Id DESC";
                        var result = await _conn.QueryAsync(query);
                        res = new
                        {
                            //res = result,   // abhilash fix
                            res = tempTable,
                            state = true
                        };
                    }
                    else
                    {
                        res = new
                        {
                            message = "No Record Found",
                            state = true
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet]
        [Route("Dash_AllRec")]
        public async Task<IActionResult> allRecord()
        {
            var query = "";
            dynamic result;
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    query = $@"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_NAME LIKE 'Template_%'";
                    result = await _conn.QueryAsync<string>(query);
                    var total = 0; query = null;
                    foreach (var data in result)
                    {
                        query = $"SELECT COUNT(*) FROM [{data}]";
                        var count = await _conn.ExecuteScalarAsync<int>(query);
                        Console.WriteLine(count);
                        total = total + count;
                    }
                    res = new
                    {
                        state = true,
                        Record = total
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet]
        [Route("Dash_OptimizedAllRec")]
        public async Task<IActionResult> OptimizedAllRecords()
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    // Get all table names matching "Template_%"
                    var tableNamesQuery = @"
                SELECT TABLE_NAME 
                FROM information_schema.TABLES 
                WHERE TABLE_NAME LIKE 'Template_%'";

                    var tableNames = await _conn.QueryAsync<string>(tableNamesQuery);

                    if (!tableNames.Any())
                    {
                        res = new { state = true, Record = 0 };
                        return Ok(res);
                    }

                    // Build UNION ALL count query
                    var countQueries = tableNames.Select(t =>
                        $"SELECT COUNT(*) AS Count FROM [{t}] WITH (NOLOCK)"
                    );

                    var fullQuery = string.Join(" UNION ALL ", countQueries);

                    // Execute the full query and sum the counts
                    var counts = await _conn.QueryAsync<int>(fullQuery);
                    int total = counts.Sum();

                    res = new
                    {
                        state = true,
                        Record = total
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }

            return Ok(res);
        }

        [HttpGet]
        [Route("Dash_ReadyTemp")]
        public async Task<IActionResult> TotalFold()
        {
            var query = "";
            dynamic result;
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    query = $@"SELECT COUNT(*) AS FilledCount FROM ImgTemplate";
                    result = await _conn.QueryAsync<int>(query);
                    res = new
                    {
                        state = true,
                        results = result
                    };
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet("Dash_Avrage")]
        public async Task<ActionResult> AvrageScore()
        {
            //object res = null;
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    await _conn.OpenAsync();
                    var query = @"SELECT TABLE_NAME FROM information_schema.Tables WHERE table_name LIKE 'Template_%'";
                    var tableNames = await _conn.QueryAsync<string>(query);
                    int totalSuccess = 0;
                    int totalFail = 0;
                    foreach (var table in tableNames)
                    {
                        query = $@"SELECT COUNT(Status) FROM {table} WHERE Status = 'True'";
                        int successCount = await _conn.ExecuteScalarAsync<int>(query);
                        totalSuccess += successCount;
                    }

                    foreach (var table in tableNames)
                    {
                        query = $@"SELECT COUNT(Status) FROM {table} WHERE Status = 'False'";
                        int failCount = await _conn.ExecuteScalarAsync<int>(query);
                        totalFail += failCount;
                    }
                    var total = totalSuccess + totalFail;
                    double successRate = (double)totalSuccess / total * 100;
                    double failRate = (double)totalFail / total * 100;
                    res = new
                    {
                        state = true,
                        message = "Average Score",
                        Succes = totalSuccess,
                        Fail = totalFail,
                        total = total,
                        Average_success = successRate + "%",
                        Average_fail = failRate + "%"
                    };

                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet("6_MonthRec")]
        public async Task<IActionResult> monthRec()
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    // get Table_list
                    await _conn.OpenAsync();
                    var query = @"SELECT TABLE_NAME FROM information_schema.Tables WHERE table_name LIKE 'Template_%'";
                    var tableNames = await _conn.QueryAsync<string>(query);

                    // Last 6 Months
                    var last6Months = new List<int>();
                    DateTime todayDate = DateTime.Now;
                    for (int i = 0; i <= 5; i++)
                    {
                        DateTime pastMonth = todayDate.AddMonths(-i);
                        last6Months.Add(pastMonth.Month);
                    }
                    
                    // Total Success and Fail
                    var grandRes = new Dictionary<string, int>();
                    foreach (var month in last6Months)   
                    {
                        var gTot = 0;
                        var fTot = 0;
                        foreach (var table in tableNames) 
                        {
                            query = $@"SELECT count([LiveTime]) FROM {table}
                            WHERE MONTH(TRY_PARSE([LiveTime] AS datetime USING 'en-GB')) = {month} AND [Status]='True'";
                            int allSuccRec = await _conn.ExecuteScalarAsync<int>(query);
                            gTot += allSuccRec;

                            query = $@"SELECT count([LiveTime]) FROM {table}
                            WHERE MONTH(TRY_PARSE([LiveTime] AS datetime USING 'en-GB')) = {month} AND [Status]='False'";
                            int allFailRec = await _conn.ExecuteScalarAsync<int>(query);
                            fTot += allFailRec;
                        }
                        var mon = new DateTime(2024, month, 1).ToString("MMMM");
                        grandRes.Add($"Month {month} as {mon} Success:", gTot);
                        grandRes.Add($"Month {month} as {mon} Failure:", fTot);
                    }
                    res = new
                    {
                        state = true,
                        message = grandRes
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet("monthSplit")]
        public async Task<IActionResult> monthSplit()
        {
            dynamic res;
            var result = "";
            try
            {
                Console.WriteLine();
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();

                    var query = @"SELECT TABLE_NAME FROM information_schema.Tables WHERE table_name LIKE 'Template_%'";
                    var tableNames = await _conn.QueryAsync<string>(query);


                    DateTime today = DateTime.Now;
                    DateTime startDate = new DateTime(today.Year, today.Month, 1);

                    List<DateTime> week1 = new List<DateTime>();
                    List<DateTime> week2 = new List<DateTime>();
                    List<DateTime> week3 = new List<DateTime>();
                    List<DateTime> week4 = new List<DateTime>();
                    for (int i = 0; i < 28; i++)
                    {
                        DateTime day = startDate.AddDays(i);
                        if (i < 7)
                            week1.Add(day);
                        else if (i < 14)
                            week2.Add(day);
                        else if (i < 21)
                            week3.Add(day);
                        else
                            week4.Add(day);
                    }
                   

                    var totWeek = new Dictionary<string, int>();

                    var week1Success = 0;
                    var week1Fail = 0;
                    foreach (var tab in tableNames)
                    {
                        foreach (var a in week1)
                        {
                            string mmdd = a.ToString("dd-MM");
                            var q1 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='True'";
                            var resultr = await _conn.ExecuteScalarAsync<int>(q1);
                            week1Success += resultr;

                            var q2 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='False'";
                            var resultr1  = await _conn.ExecuteScalarAsync<int>(q2);
                            week1Fail += resultr1;
                        }

                    }
                    totWeek.Add("Week1 Success:", week1Success);
                    totWeek.Add("Week1 Failure:", week1Fail);

                    var week2Success = 0;
                    var week2Fail = 0;
                    foreach (var tab in tableNames)
                    { 
                        foreach (var a in week2)
                        {
                            string mmdd = a.ToString("dd-MM");
                            var q1 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='True'";
                            var resultr = await _conn.ExecuteScalarAsync<int>(q1);
                            week2Success += resultr;

                            var q2 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='False'";
                            var resultr1 = await _conn.ExecuteScalarAsync<int>(q2);
                            week2Fail += resultr1;
                        }
                    }
                    totWeek.Add("Week2 Success:", week2Success);
                    totWeek.Add("Week2 Failure:", week2Fail);

                    var week3Success = 0;
                    var week3Fail = 0;
                    foreach (var tab in tableNames)
                    { 
                        foreach (var a in week3)
                        {
                            string mmdd = a.ToString("dd-MM");
                            var q1 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='True'";
                            var resultr = await _conn.ExecuteScalarAsync<int>(q1);
                            week3Success += resultr;
                            var q2 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='False'";
                            var resultr1 = await _conn.ExecuteScalarAsync<int>(q2);
                            week3Fail += resultr1;
                        }
                    }
                    totWeek.Add("Week3 Success:", week3Success);
                    totWeek.Add("Week3 Failure:", week3Fail);
                    
                    var week4Success = 0;
                    var week4Fail = 0;
                    foreach (var tab in tableNames)
                    { 
                        foreach (var a in week4)
                        {
                            string mmdd = a.ToString("dd-MM");
                            var q1 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='True'";
                            var resultr = await _conn.ExecuteScalarAsync<int>(q1);
                            week4Success += resultr;

                            var q2 = $@"select count(*) from {tab} where [LiveTime] Like '{mmdd}%' AND [Status]='False'";
                            var resultr1 = await _conn.ExecuteScalarAsync<int>(q2);
                            week4Fail += resultr1;
                        }
                    }
                    totWeek.Add("Week4 Success:", week4Success);
                    totWeek.Add("Week4 Failure:", week4Fail);

                    res = new
                    {
                        state = true,
                        firstWeekS = week1Success,
                        firstWeekF = week1Fail,
                        secondWeekS = week2Success,
                        secondWeekF = week2Fail,
                        ThirdweekS = week3Success,
                        ThirdWeekF = week3Fail,
                        FourthWeekS = week4Success,
                        FourthWeekF = week4Fail
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpDelete("AllRecDel")]
        public async Task<IActionResult> AllRecDel(string folderPath= "uttarkhandold", int idTemp=51)
        {
            dynamic res;
            string querry;
            try {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var Temp = "Template_" + idTemp;
                    var Successfolder = Path.Combine(_env.ContentRootPath + "/wwwroot" + "/ScannedImg/" + Temp);
                    if (!Directory.Exists(Successfolder))
                    {
                        Directory.CreateDirectory(Successfolder);
                    }
                    var Failurefolder = Path.Combine(_env.ContentRootPath + "/wwwroot" + "/RejectImg/" + Temp);
                    if (!Directory.Exists(Failurefolder))
                    {
                        Directory.CreateDirectory(Failurefolder);
                    }
                    var RootfolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager/" + folderPath);
                    if (!Directory.Exists(RootfolderPath))
                    {
                        Directory.CreateDirectory(RootfolderPath);
                    }
                    try
                    {
                        var SuccFiles = Directory.GetFiles(Successfolder, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")).ToList();
                        var FailFiles = Directory.GetFiles(Failurefolder, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")).ToList();
                        foreach (var img in SuccFiles)
                        {
                            if (!System.IO.File.Exists(img))
                                return NotFound();
                            var fileName = Path.GetFileName(img);
                            var desti = Path.Combine(RootfolderPath, fileName);
                            if (System.IO.File.Exists(desti))
                            {
                                System.IO.File.Delete(desti);
                            }
                            System.IO.File.Move(img, desti);
                        }
                        foreach (var img in FailFiles)
                        {
                            if (!System.IO.File.Exists(img))
                                return NotFound();
                            var fileName = Path.GetFileName(img);
                            var desti = Path.Combine(RootfolderPath, fileName);
                            if (System.IO.File.Exists(desti))
                            {
                                System.IO.File.Delete(desti);
                            }
                            System.IO.File.Move(img, desti);
                        }

                        querry = $"DELETE {Temp}";
                        var result = await _conn.ExecuteAsync(querry);
                        res = new
                        {
                            state = true,
                            message = "All Records Deleted Successfully and moved",
                            count = result
                        };
                    }
                    catch (DirectoryNotFoundException)
                    {
                        res = new
                        {
                            state = false,
                            massage = "Files Not Found"
                        };
                    }
                    catch (Exception ex)
                    {
                        res = new
                        {
                            state = false,
                            massage = ex.Message
                        };
                    }

                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpGet("GetDB_Rec")]
        public async Task<IActionResult> GetDB_Rec(string? fileName)
        {
            dynamic res;
            var result = "";
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var querry = "";
                    dynamic queryResult;
                    if (fileName==null)
                    {
                        querry = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES where TABLE_NAME LIKE 'Template%';";
                        queryResult = _conn.Query(querry);

                    }
                    else
                    {
                        querry = $@"SELECT * FROM [{fileName}];";
                        queryResult = _conn.Query(querry);
                    }



                res = new
                    {
                        state = true,
                        queryResult
                    };

                    _conn.Close();
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };

            }
            return Ok(res);
        }

        [HttpDelete("Delete_Rec")]
        public async Task<IActionResult> Delete_Rec(string deleteName)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var querry = $@"DROP TABLE [{deleteName}]";
                    var result = _conn.Execute(querry);
                    res = new
                    {
                        state = true,
                        rec = "Table Delete form DataBase"
                    };
                }
            }
            catch (Exception ex) {

                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            return Ok(res);
        }
    }
}











