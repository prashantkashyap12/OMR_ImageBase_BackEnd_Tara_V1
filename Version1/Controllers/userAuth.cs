using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Dapper;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Anssi;
using Org.BouncyCastle.Ocsp;
using SixLabors.ImageSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Syncfusion.EJ2.Notifications;
using Version1.Data;
using Version1.Modal;
using ZXing.Aztec.Internal;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    [ApiController]
    public class userAuth : ControllerBase
    {
        private readonly ApplicationDbContext _DbContext;
        private readonly JwtAuth _jwtTokenGen;
        private readonly EncptDcript _EncptDcript;
        private readonly IConfiguration _conn;
        private readonly string _connectionString;
        private readonly EmailSendClass _emailSendClass;
        private readonly ImgSave _imgSave;
        private readonly ILogger _logger;

        public userAuth(ApplicationDbContext DbContext, JwtAuth Jwt, EncptDcript EncDec, IConfiguration conn, EmailSendClass emailSendClass, ImgSave imgSave, ILogger<userAuth> logger)
        {
            _DbContext = DbContext;
            _jwtTokenGen = Jwt;
            _EncptDcript = EncDec;
            _conn = conn;
            _connectionString = conn.GetConnectionString("dbc");
            _emailSendClass = emailSendClass;
            _imgSave = imgSave;
            _logger = logger;
        }

        [HttpGet("DeviceLogOut")]
        public async Task<IActionResult> allDeviceLogOut()
        {
            dynamic res;
            var qurry = "";
            var expiredToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            if (string.IsNullOrWhiteSpace(expiredToken))
            {
                return Unauthorized(new { message = "No token provided" });
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(expiredToken);
            var empId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
            var empName = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
            var empEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var Phone = jwtToken.Claims.FirstOrDefault(c => c.Type == "Phone")?.Value;
            var refranceId = jwtToken.Claims.FirstOrDefault(c => c.Type == "refranceId")?.Value;

            var emp = new EmpModel
            {
                EmpId = empId,
                EmpName = empName,
                EmpEmail = empEmail,
                role = role,
                RefranceId = refranceId,
                contact = Phone 
            };
            var jwtAuth = _jwtTokenGen.GenerateJwtToken(emp);

            using (var _conn = new SqlConnection(_connectionString))
            {
                var TokenMain = jwtAuth;
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken2 = tokenHandler.ReadJwtToken(jwtAuth);
                var EmpId2 = jwtToken2.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                var role2 = jwtToken2.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                var expiryTimeUtc1 = jwtToken.ValidFrom.ToLocalTime();
                string expiryTimeUtc2 = jwtToken.ValidTo.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
                qurry = $@"update LoginTokenRec set isLoggedIn='0' where EmpId = {EmpId2}";
                var result = await _conn.ExecuteAsync(qurry);
                res = new
                {
                    state = true,
                    message = $"Logout From all Devices",
                    token = TokenMain,
                };
            }
            return Ok(res);
        }

        [HttpGet("RefreshToken")]
        public async Task<IActionResult> refresh()
        {
            dynamic res;
            var expiredToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            if (string.IsNullOrWhiteSpace(expiredToken))
            {
                return Unauthorized(new { message = "No token provided" });
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken1 = handler.ReadJwtToken(expiredToken);
            var empId = jwtToken1.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            var role = jwtToken1.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
            var empName = jwtToken1.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
            var empEmail = jwtToken1.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var Phone = jwtToken1.Claims.FirstOrDefault(c => c.Type == "Phone")?.Value;
            var Refrance = jwtToken1.Claims.FirstOrDefault(c => c.Type == "refranceId")?.Value;
            var emp = new EmpModel
            {
                EmpId = empId,
                EmpName = empName,
                EmpEmail = empEmail,
                role = role,
                contact = Phone,
                RefranceId = Refrance
            };

            var ReturnDetails = _DbContext.empModels.FirstOrDefault(x => x.EmpEmail == empEmail);

            var jwtAuth = _jwtTokenGen.GenerateJwtToken(ReturnDetails);

            using (var _conn = new SqlConnection(_connectionString))
            {
                _conn.Open();
                var TokenMain = jwtAuth;
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(TokenMain);
                var EmpId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                var role1 = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                string expiryTimeUtc2 = jwtToken.ValidTo.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
                var qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeUtc2}',Role='{role1}', isLoggedIn=1 where EmpId = {EmpId}";
                var result = await _conn.ExecuteAsync(qurry);
                _conn.Close();
            }
            res = new
            {
                state = true,
                token = jwtAuth
            };
            return Ok(res);
        }

        [HttpPost]
        [Route("GoogleToken")]
        public async Task<IActionResult> GoogleAsync(string googleToken)
        {
            if (!string.IsNullOrEmpty(googleToken))
            {
                // 1. Google Verify Token 

                // 2. Find Email using EF

                // 3. Registraion By Static Value for incompleteDetails "GVerify"

                // 4. Then Create Token JWT Retutn Back


            }
            return Ok();
        }

        // Add API --  
        [HttpPost("SignUp")]
        public async Task<IActionResult> Add(string name, string email, string pwd, string cont, string role, string ?refranceId)
        {
            dynamic res;
            try {
                dynamic resp;
                string query;
                bool isEmt = false;
                StringBuilder sb = new StringBuilder();
                using (var _conn = new SqlConnection(_connectionString))
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        sb.Append("Name, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(email))
                    {
                        sb.Append("Email, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(pwd))
                    {
                        sb.Append("Password, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(cont))
                    {
                        sb.Append("Contact, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(role))
                    {
                        sb.Append("Role, ");
                        isEmt = true;
                    }
                    if (isEmt == true)
                    {
                        res = new
                        {
                            state = false,
                            Message = @$"These value are Empty {sb}"
                        };
                    }
                    else
                    {
                        var emailExist = _conn.ExecuteScalarAsync<int?>($@"select COUNT(1) from empModels where EmpEmail = '{email}'");
                        if (emailExist.Result != 0) {
                            res = new
                            {
                                state = false,
                                Message = @$"Email Id is Already Exist {email}"
                            };
                        }
                        else {
                            query = $@"select MAX(EmpId) from empModels";
                            var result = _conn.ExecuteScalarAsync<int?>(query);
                            var empId = result.Result == null ? 1001 : result.Result + 1;

                            // ****   Create OTP -- OPEN     ****
                            var SixOTP = await _emailSendClass.emailRec(email);
                            var Otp = SixOTP.ToString();

                            var query2 = @$"insert into empModels ([EmpName],[EmpEmail],[password],[contact],[role], [EmpId], [UserOtp],[refranceId]) values('{name}', '{email}','{pwd}', '{cont}', '{role}', {empId}, '{Otp}','{refranceId}'); 
                            Insert into LoginTokenRec (EmpId) values ('{empId}')";
                            var result2 = await _conn.ExecuteAsync(query2);
                            
                            var waletUpdate = await _conn.ExecuteAsync($"insert into Wallet (Uid, TimeDate, CreditLimit) values (@empId, @UpdatedDate, '0')", new { empId = empId, UpdatedDate = DateTime.Now });

                            res = new
                            {
                                state = true,
                                Message = @$"User Created Successfully {email}"
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    Message = ex.Message,
                    status = false,
                };
            }
            return Ok(res);
        }

        // API Done
        [HttpPost("SignUpMob")]
        public async Task<IActionResult> Add2(string name, string email, string cont)
        {
            dynamic res;
            try
            {
                var already = _DbContext.empModels.FirstOrDefault(a => a.EmpEmail == email);
                if (already != null)
                {
                    res = new
                    {
                        state = false,
                        Message = "This Email Already Exist"
                    };
                }
                else
                {
                    using (var _conn = new SqlConnection(_connectionString))
                    {
                        _conn.Open();
                        bool isEmt = true;
                        if (string.IsNullOrEmpty(name))

                        {
                            isEmt = false;
                        }
                        if (string.IsNullOrEmpty(email))
                        {
                            isEmt = false;
                        }
                        if (string.IsNullOrEmpty(cont))
                        {
                            isEmt = false;
                        }

                        var query = $@"select MAX(EmpId) from empModels";
                        var result = _conn.ExecuteScalarAsync<int?>(query);
                        var empId = result.Result == null ? 1001 : result.Result + 1;
                        var SixOTP = await _emailSendClass.emailRec(email);
                        var RefranceId = $"OMR{empId}{DateTime.Now:yyyyMMddHHmmss}";
                        var emailRec = new EmpModel
                        {
                            EmpId = empId.ToString(),
                            EmpName = name,
                            EmpEmail =email,
                            password = "STPI",
                            contact = cont,
                            role = "admin",
                            IsLoggedIn = false,
                            UserOtp = SixOTP,
                            RefranceId = RefranceId
                        };
                        var data = _DbContext.empModels.Add(emailRec);
                        await _DbContext.SaveChangesAsync();
                        var dtRes = await _conn.ExecuteAsync($"Insert into LoginTokenRec(EmpId) values(@EempId)", new { EempId = empId });
                        var isExist = await _conn.QueryFirstOrDefaultAsync($"select * from Wallet where Uid = {empId}");
                        if (isExist == null)
                        {
                            var waletUpdate = await _conn.ExecuteAsync($"insert into Wa" +
                                $"llet (Uid, TimeDate, CreditLimit, reneration_results, recognition_credits_total, generation_results_total) values (@empId, @UpdatedDate, '0', 0, 0, 0)", new { empId = empId, UpdatedDate = DateTime.Now });
                        }
                        res = new
                        {
                            state = true,
                            message = $"Sent OTP Successfully at {email}"
                        };
                    }
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

        // Retireve API --  All Record
        [HttpPost]
        [Route("ForgetRequest")]
        public async Task<IActionResult> ForgetRequest(string Email)
        {
            dynamic res;
            try
            {
                var EmpRec = _DbContext.empModels.FirstOrDefault(a => a.EmpEmail == Email);
                if (EmpRec == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Record Not Found"
                    };
                }
                else
                {
                    // ****   Create OTP -- OPEN     ****
                    var SixOTP = await _emailSendClass.emailRec(Email);
                    var Otp = SixOTP.ToString();
                    Otp = Otp ?? "654321";
                    EmpRec.UserOtp = Otp;


                    var updateDb = _DbContext.empModels.Update(EmpRec);
                    await _DbContext.SaveChangesAsync();
                    res = new
                    {
                        state = true,
                        Message = "OTP Send SuccessFully"
                    };
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                  state = false,
                  Message = Ex.Message
                };
            }

            return Ok(res);
        }

        // Retireve API --  All Record
        [HttpPost]
        [Route("Verify")]
        public async Task<IActionResult> Verify(string email, string Otp)
        {
            dynamic res;
            try
            {
                var dataFind = _DbContext.empModels.FirstOrDefault(a=> a.EmpEmail == email && a.UserOtp == Otp);
                var token1 = "";
                if (dataFind == null)
                {
                    return res = new
                    {
                        state = false,
                        message = "Record Not Found"
                    };
                }
                if (dataFind.UserOtp == Otp)
                {
                    dataFind.IsLoggedIn = true;
                    token1 = _jwtTokenGen.GenerateJwtToken(dataFind);
                    res = new
                    {
                        state = true,
                        Message = "User Verifyed",
                        Record = dataFind,
                        Token = token1,
                    };
                }
                else
                {
                    dataFind.IsLoggedIn = false;
                    res = new
                    {
                        state = false,
                        Message = "Not Verifyed"
                    };
                }
                _DbContext.empModels.Update(dataFind);
                await _DbContext.SaveChangesAsync();


                string qurry = null;
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var TokenMain = token1;
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(TokenMain);
                    var EmpId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                    var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                    string expiryTimeUtc2 = jwtToken.ValidTo.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
                    qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeUtc2}',Role='{role}', isLoggedIn=1 where EmpId = {EmpId}";
                    var result = await _conn.ExecuteAsync(qurry);
                }
            }
            catch (Exception Ex)
            {
                res = new
                {
                    state = false,
                    Message = Ex.Message
                };
            }
            
            return Ok(res);
        }

        // Retireve API --  All Record
        [HttpPost]
        [Route("GetList")]
        public async Task<IActionResult> GetList()
        {
            dynamic res;
            try {
                var empList = _DbContext.empModels.ToList();
                _logger.LogInformation($"Get Succsfully ={empList}");
                if (!empList.Any())
                {
                    res = new
                    {
                        message = @$"No Record",
                        state = false
                    };
                }
                else 
                {
                    res = new
                    {
                        result = empList,
                        state = true
                    };
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            return Ok(res);
        }

        // Delete API  -- All Record
        [HttpDelete]
        [Route("DeleteEmp")]
        public async Task<ActionResult> delete(string idEmp)
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
                var EmpidVal = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                _logger.LogTrace($"Method Started={EmpidVal}");
                using (var _conn = new SqlConnection(_connectionString))
                {
                    var Empid = _conn.QueryFirstOrDefault<int>($"select EmpId from empModels where EmpId = '{idEmp}'");
                    if (Empid == null)
                    {
                        res = new
                        {
                            state = false,
                            message = "Record is not avilable"
                        };
                    }
                    else
                    {
                        var query = $@"delete LoginTokenRec where EmpId = {Empid}; delete empModels where EmpId = {Empid}";
                        var ress = await _conn.ExecuteAsync(query);
                        Console.WriteLine(ress);
                        res = new
                        {
                            state = true,
                            result = "Deleted Record"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, ex.Message);
               res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            return Ok(res);
        }

        // Update API  -- Update User
        [HttpPut]
        [Route("Update")]
        public IActionResult update(string EmpId, string name, string email, string pwd, string cont, string role)
        {
            //var hashingPwd = _empService.ComputeSha256Hash(pwd);
            dynamic res;
            try
            {
                var idmain = _DbContext.empModels.Find(EmpId);
                if (idmain == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Record not Find"
                    };
                }
                else
                {
                    idmain.EmpName = name;
                    idmain.EmpEmail = email;
                    idmain.password = pwd;
                    idmain.contact = cont;
                    idmain.role = role;
                    _DbContext.SaveChanges();
                    res = new
                    {
                        state = true,
                        message = @$"Record saved {email}"
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
            return Ok(res);
        }

        // Check does Login oR not
        [HttpPost]
        [Route("LoginForm")]
        public async Task<IActionResult> get(string uname, string pwd)
        {
            dynamic res;
            bool isVerify = true;
            try
            { 
                var token = string.Empty;
                //checked validation
                if (string.IsNullOrWhiteSpace(uname) || string.IsNullOrWhiteSpace(pwd))
                {
                    res = new
                    {
                        state = false,
                        message = "Please Fill Details"
                    };
                }

                var ReturnDetails = _DbContext.empModels.FirstOrDefault(x => x.EmpEmail == uname);
                if (ReturnDetails == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Email not found.",
                    };
                }
                else
                {
                    if (ReturnDetails.password != pwd)
                    {
                        res = new
                        {
                            state = false,
                            message = $@"Password not Match with {uname}"
                        };
                    }
                    else
                    {
                        token = _jwtTokenGen.GenerateJwtToken(ReturnDetails);   
                        string qurry = null;
                        using (var _conn = new SqlConnection(_connectionString))
                        {
                            _conn.Open();
                            var TokenMain = token;
                            var tokenHandler = new JwtSecurityTokenHandler();
                            var jwtToken = tokenHandler.ReadJwtToken(token);
                            var EmpId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                            string expiryTimeUtc2 = jwtToken.ValidTo.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
                            qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeUtc2}',Role='{role}', isLoggedIn=1 where EmpId = {EmpId}";
                            var result = await _conn.ExecuteAsync(qurry);
                        }
                        res = new
                        {
                            state = true,
                            message = $"Login Success",
                            token = token,
                        };
                    }
                }
            }
            catch(Exception ex) {
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

        // Update Profile
        [HttpPost]
        [Route("updateProfiles")]
        public async Task<IActionResult> updateProfiles(updateProfile model)
        {
            dynamic res;

            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    _logger.LogTrace("Method Started");
                    var resp = _conn.QueryFirstOrDefault($"select * from empModel2 where EmpId = {model.EmpId}");
                    var already = _DbContext.empModels.FirstOrDefault(a => a.EmpId == model.EmpId);
                    if (already != null)
                    {
                        res = new
                        {
                            status = true,
                            message = "User Not Found"
                        };
                    }
                    else
                    {
                        //userprofile
                        var dataData = await _imgSave.userprofile(model.EmpId, model.Updateimage);
                        var sql = @"INSERT INTO empModel2 (EmpId, imageName, text1, text2, text3, text4, text5, text6)
                        VALUES (@EmpId, @Updateimage, @text1, @text2, @text3, @text4, @text5, @text6)";
                        var RespData = _conn.Execute(sql, new
                        {
                            model.EmpId,
                            model.Updateimage,
                            model.text1,
                            model.text2,
                            model.text3,
                            model.text4,
                            model.text5,
                            model.text6
                        });
                        res = new
                        {
                            status = true,
                            message = RespData
                        };
                    }
                    _conn.Close();
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

        public class updateProfile
        {
            public string EmpId { set; get; } = "";
            public IFormFile Updateimage { get; set; }
            public string text1 { get; set; } = "";
            public string text2 { get; set; } = "";
            public string text3 { get; set; } = "";
            public string text4 { get; set; } = "";     
            public string text5 { get; set; } = ""; 
            public string text6 { get; set; } = "";
        }

    }
}
