using System.Dynamic;
using System.IdentityModel.Tokens.Jwt;
using System.Transactions;
using Dapper;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Org.BouncyCastle.Bcpg;
using SQCScanner.Modal.Wallet;
using Syncfusion.EJ2.Notifications;
using static OpenCvSharp.FileStorage;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WalletController : ControllerBase
    {


        public readonly IConfiguration _connfigraion;
        public readonly string connecitonString;


        public WalletController(IConfiguration connfigraion)
        {
            _connfigraion = connfigraion;
            connecitonString = _connfigraion.GetConnectionString("dbc")!;
        }


        [HttpPost]
        [Route("AddPackage")]
        public async Task<IActionResult> AddPackage(PackagesApi data)
        {
            try
            {
                using (var _conn = new SqlConnection(connecitonString))
                {
                    await _conn.OpenAsync();

                    using (var _tran = _conn.BeginTransaction())
                    {
                        try
                        {
                            var lastRes = await _conn.ExecuteScalarAsync<int?>(
                                "SELECT MAX(PackId) FROM Packages",
                                transaction: _tran);

                            var packId = (lastRes ?? 1000) + 1;

                            await _conn.ExecuteAsync(
                                @"INSERT INTO Packages
                                  (PackId, PackageName, SubHeading, creditLimit, Amount)
                                  VALUES
                                  (@PackId, @PackageName, @SubHeading, @creditLimit, @Amount)",
                                new
                                {
                                    PackId = packId,
                                    data.PackageName,
                                    data.SubHeading,
                                    data.creditLimit,
                                    data.Amount
                                },
                                _tran);

                            if (data.packageList != null && data.packageList.Count > 0)
                            {
                                foreach (var item in data.packageList)
                                {
                                    await _conn.ExecuteAsync(
                                    @"INSERT INTO PackageList
                                    (PackId, ButtlePints)
                                    VALUES
                                    (@PackId, @BulletPointss)",
                                    new
                                    {
                                        PackId = packId,
                                        BulletPointss = item.BulletPoints
                                    },
                                    _tran);
                                }
                            }

                            await _tran.CommitAsync();

                            return Ok(new
                            {
                                state = true,
                                PackId = packId,
                                message = "Record Added Successfully"
                            });
                        }
                        catch
                        {
                            await _tran.RollbackAsync();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    state = false,
                    message = ex.Message
                });
            }
        }

        [HttpDelete]
        [Route("DeletePackage")]
        public async Task<IActionResult> DeletePackage(int PackId)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(connecitonString))
                {
                    _conn.Open();
                    var isPAckage = _conn.Query($"select * from Packages where PackId ={PackId}").ToList();
                    if (isPAckage.Count!=0)
                    {
                        var querry = $"delete Packages where PackId ={PackId};delete PackageList where PackId ={PackId};";
                        var resp = await _conn.ExecuteAsync(querry);
                        res = new
                        {
                            state = true,
                            message = "Record Delete Successfully"
                        };
                    }
                    else
                    {
                        res = new
                        {
                            state = false,
                            message = "Record Not Found"
                        };
                    }
                }
            }catch(Exception Ex)
            {
                res = new
                {
                    state = false,
                    message = Ex.Message
                };
            }
            return Ok(res);
        }

        // *** User wise informations ***
        // - All package
        // - Active package, Credit Limits
        [HttpGet("ListPackages")]
        public async Task<IActionResult> GetDataRecord()
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
                Console.WriteLine(Empid);
                using (var _conn = new SqlConnection(connecitonString))
                {
                    await _conn.OpenAsync();
                    var data = await _conn.QueryAsync("select * from Packages as pk left join PackageList as pkls on pk.PackId = pkls.PackId");
                    var result = data
                    .GroupBy(x => x.PackId)
                    .Select(g => new
                    {   PackId = g.First().PackId,
                        PackageName = g.First().PackageName,
                        SubHeading = g.First().SubHeading,
                        Amount = g.First().Amount,
                        creditLimit = g.First().creditLimit,
                        ButtlePints = g.Select(x => (string)x.ButtlePints).Where(x => !string.IsNullOrEmpty(x)).ToList()
                    }).ToList();

                    var currentPack = await _conn.QueryFirstOrDefaultAsync($"select TOP 1 [PackageId] from [PaymentOrders] where EmpId = '{Empid}' ORDER BY UpdatedDate DESC");
                    currentPack = currentPack ?? "User Not Found";
                    res = new ExpandoObject();
                    dynamic creditLimitss = 0;
                    dynamic packageId = 0;
                    dynamic Currentpackage = 0;
                    if (currentPack != "User Not Found")
                    {
                        creditLimitss = await _conn.QueryFirstOrDefaultAsync($"select * from Wallet where Uid = '{Empid}'");
                        packageId = currentPack.PackageId;
                        Currentpackage = result.Find(a => a.PackId == packageId);
                        res.activePackage = new
                        {
                            pack_no = packageId ?? 0,                               // Current Package ID
                            recognition_credits = creditLimitss.CreditLimit ?? 0,   // Over all limit ++wallet
                            CurrentPackage = Currentpackage.PackageName ?? 0,       // Current Package Name
                            reneration_results = 5,                                 
                            recognition_credits_total = 100,               
                            generation_results_total = 50,                          // Total Successfull OMR Sheet Results 
                        };
                    }
                    res.status = true;
                    res.mesaage = "Package fatched";
                    res.data = result;
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                    status = true,
                    mesaage = Ex.Message
                };
            }
            return Ok(res);
        }
        
        
        
        public class PackagesApi 
        {
            public int packId {get; set;}
            public string PackageName { get; set; }
            public string SubHeading { get; set; }
            public int creditLimit { get; set; }
            public int Amount  { get; set; }
            public List<packageList> packageList { get; set; }
        }
        public class packageList
        {
            public int packId { get; set; } = 0;
            public string BulletPoints { get; set; } = "";

        }

        [HttpPost]
        [Route("RechargeWallet")]
        public async Task<IActionResult> UpdateWallet(RechargeWallet Data)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(connecitonString))
                {
                    _conn.Open();
                    var querry = $"insert into [Transaction] Values('{Data.TranDate}', '{Data.TranId}', '{Data.EmpId}', {Data.packageId}, {Data.Amount})";
                    var resp = await _conn.ExecuteAsync(querry);

                    if (resp == 1)
                    {
                        var getLimit = _conn.QueryFirstOrDefault<string>("SELECT creditLimit FROM Packages WHERE PackId = @PackId", new { PackId = Data.packageId });
                        var WalletUpdate = await _conn.ExecuteAsync($"insert into [Wallet] values ('{Data.EmpId}', '{Data.TranDate}', {getLimit})");
                    }
                    res = new
                    {
                        state = true,
                        message = "Record updated Successfully",
                        Date = Data
                    };
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                    state = false,
                    message = Ex.Message
                };
            }
            return Ok(res);
        }

        [HttpGet]
        [Route("GetWalletTran")]
        public async Task<IActionResult> GetWalletTran(string? EmpId)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(connecitonString))
                {

                    _conn.Open();
                    var querry = "select * from [Transaction]";
                    if (EmpId != null)
                    {
                        querry += $" where EmpId = '{EmpId}'";
                    }
                    var data = await _conn.QueryAsync(querry);
                    res = new
                    {
                        state = true,
                        message = "Data Fatched",
                        Data = data
                    };
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                    state = false,
                    message = Ex.Message
                };
            }
            return Ok(res);
        }

        [HttpGet]
        [Route("GetWallet")]
        public async Task<IActionResult> GetWallet(string? EmpId)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(connecitonString))
                {

                    _conn.Open();
                    var querry = "select * from [Wallet]";
                    if (EmpId != null)
                    {
                        querry += $" where Uid = '{EmpId}'";
                    }
                    var data = await _conn.QueryAsync<int>(querry);
                    res = new
                    {
                        state = true,
                        message = "Data Fatched",
                        Data = data
                    };
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                    state = false,
                    message = Ex.Message
                };
            }
            return Ok(res);
        }

        [HttpGet]
        [Route("PaymentHistory")]
        public async Task<IActionResult> GetPaymentHistory()
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

                using (var _conn = new SqlConnection(connecitonString))
                {
                    _conn.Open();

                    var querry = $"select * from PaymentOrders where EmpId = {Empid}";
                    var result = _conn.Query(querry);
                    res = new
                    {
                        status = true,
                        data = result
                    };
                    _conn.Close();
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    status = true,
                    message = ex.Message 
                };
            }

            return Ok(res);
        }

        public class RechargeWallet
        {
            public DateTime TranDate { set; get; }
            public String TranId { set; get; } = "";
            public string EmpId { set; get; } = "";
            public int packageId { set; get; }
            public decimal Amount { set; get; }
        };
    }
}
