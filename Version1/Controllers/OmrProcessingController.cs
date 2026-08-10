using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Version1.Data;
using Version1.Modal;
using Version1.Services;
using SQCScanner.Services;
using SQCScanner.websoketManager;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OpenCvSharp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using System.Text;

namespace Version1.Controllers
{
    [Authorize]
    [EnableCors("AllowAnyOrigin")]
    [Route("api/[controller]")]
    [ApiController]
    public class OmrProcessingController : ControllerBase
    {
        private readonly OmrProcessingService _omrService;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _dbContext;
        private readonly WebSoketHandler _webSocketHandler;
        private readonly OmrProcessingControlService _controlService;
        private readonly RecordSave _SaveOnly;
        private readonly table_gen _recordTable;
        private readonly ImgSave _imgSave;
        private readonly FindCordinationClass _FindCordinationClass;

        public OmrProcessingController(
            OmrProcessingService omrService,
            IWebHostEnvironment env,
            ApplicationDbContext dbContext,
            WebSoketHandler webSocketHandler,
            OmrProcessingControlService controlService,
            RecordSave recordSave,
            table_gen recordTable,
            ImgSave imgSave,
            FindCordinationClass FindCordinationClass)
            {
            _omrService = omrService;
            _env = env;
            _dbContext = dbContext;
            _recordTable = recordTable;
            _webSocketHandler = webSocketHandler;
            _controlService = controlService;
            _SaveOnly = recordSave;
            _imgSave = imgSave;
            _FindCordinationClass = FindCordinationClass;
                if (controlService == null)
                {
                    throw new ArgumentNullException(nameof(controlService), "OmrProcessingControlService is not injected properly.");
                }
            }

        //  Process OMR Sheet    
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(string folderPath, string token, int idTemp, bool IsSaveDb, bool failReScan = true)
        {
            dynamic resp;
            _controlService.ResetProcessing();


            // Token handler UserId Extract
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
            var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;
            var folderPAth = folderPath;


            // Y/N = ReScan failure Img Folder.
            var sharefolder = "";
            if (failReScan)
            {
                sharefolder = Path.Combine("wFileManager/" + folderPath);
            }
            else
            {
                sharefolder = Path.Combine("RejectImg/" + folderPath);
            }

            // Exist path
            folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager/" + folderPath);
                if (!Directory.Exists(folderPath))
            {
                resp = new
                {
                    state = false,
                    message = "Folder path is invalid"
                };
            }
            else
            {
                var imageFiles = Directory.GetFiles(folderPath, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg") || f.EndsWith(".tif")).ToList();
                var Targetjson = string.Empty;
                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == idTemp);
                string imageUrl = ReturnDetails.imgPath;
                string templateName = ReturnDetails.FileName;
                imageUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl);
                if (ReturnDetails != null)
                {
                    if (!string.IsNullOrEmpty(ReturnDetails.JsonPath))
                    {
                        Targetjson = ReturnDetails.JsonPath.Replace("\\", "/");
                        string templatePath = Path.Combine(_env.WebRootPath, Targetjson);
                        var results = new List<OmrResult>();
                        var crttb = 1;
                        var totalCount = JsonSerializer.Serialize(imageFiles.Count);

                        if (imageFiles.Count == 0)
                        {
                            resp = new
                            {
                                state = false,
                                message = "Image is not found"
                            };
                        }
                        else
                        {
                            int ser = 0;
                            foreach (var imagePath in imageFiles)
                            {
                                ser = ser + 1;
                                 // Stop and Continue API Globle 
                                _controlService.WaitIfPaused();

                                // Stop handle
                                if (_controlService.IsStopRequested)
                                {
                                    break;
                                }

                                // Scaning to get data from OMR Sheet
                                var res = await _omrService.ProcessOmrSheet(imagePath, templatePath, imageUrl, ser, userName);
                                results.Add(res);
                                if (true)
                                {
                                    if (crttb == 1)
                                    {
                                        var tableCrt = await _recordTable.TableCreation(res, idTemp);
                                    }
                                    crttb++;
                                }
                                dynamic dbRes = null;

                                // 1. Save_Record into DB         - Done 
                                dbRes = await _SaveOnly.RecordSaveVal(res, idTemp, userName, IsSaveDb, folderPAth, imagePath, templateName);
                                if (IsSaveDb)
                                {
                                // 2. Save_Sacanned Img Folder    - Done
                                    var stat = res.Success;
                                    var SaveRoot = await _imgSave.ScanedSave(_env.WebRootPath, imagePath, idTemp, stat);
                                }
                                // 3. WS_Handler                  - Done
                                string jsonResult = JsonSerializer.Serialize(dbRes);
                                userId = Convert.ToString(userId);
                                await _webSocketHandler.UserMessageAsync(userId, jsonResult);
                            }
                            await _webSocketHandler.UserMessageAsync(userId, totalCount);
                            await _webSocketHandler.UserMessageAsync(userId, "");

                            // Download CSV 
                            var jsonString = JsonSerializer.Serialize(results);
                            var csvBytes = Encoding.UTF8.GetBytes(jsonString);
                            resp = new
                            {
                                state = true,
                                record = results,
                                csv = csvBytes
                            };
                        }
                    }
                    else
                    {
                        resp = new
                        {
                            state = false,
                            message = "Template not found"
                        };
                    }
                }
                else
                {
                    resp = new
                    {
                        state = false,
                        message = "Id is invalid please add Template first"
                    };
                }
            }
            bool currentState = resp.state;
            if (currentState)
            {
                return Ok(resp);
            }
            else
            {
                return NotFound(resp);
            }
        }

        // Data Push procesing
        [HttpPost("pause-processing")]
        public IActionResult PauseProcessing()
        {
            _controlService.PauseProcessing();
            return Ok("Processing paused.");
        }

        [HttpPost("resume-processing")]
        public IActionResult ResumeProcessing()
        {
            _controlService.ResumeProcessing();
            return Ok("Processing resumed.");
        }


        [HttpPost("stop-processing")]
        public IActionResult StopProcessing()
        {
            _controlService.StopProcessing();
            return Ok("Processing stopped.");
        }
        
        [HttpPost("findCrodination")]
        public async Task<IActionResult> findCordination(IFormFile TestImage, IFormFile jsonStract)
        {
            if (TestImage == null || jsonStract == null)
            {
                return BadRequest("Both image and JSON structure are required.");
            }

            string imageUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"marked_{Guid.NewGuid()}Image.png");
            string JsonUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"marked_{Guid.NewGuid()}JSON.png");

            using (var fileStream = new FileStream(imageUrl, FileMode.Create))
            {
                await TestImage.CopyToAsync(fileStream);
            }

            using (var fileStream1 = new FileStream(JsonUrl, FileMode.Create))
            {
                await jsonStract.CopyToAsync(fileStream1);
            }


            // Call the method to process the image and JSON files
            var resultMatrix = await _FindCordinationClass.FindCordinationAsync(imageUrl, JsonUrl);
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var fileStream = new FileStream(resultMatrix, FileMode.Open))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
                imageBytes = memoryStream.ToArray();     
            }
            return File(imageBytes, "image/png", "processed_image.png");
        }

        [HttpPost("GetCSVHeader")]
        public async Task<IActionResult> getHeader(IFormFile CSV1)
        {

            if (CSV1 == null || CSV1.Length == 0)
                return BadRequest("File not found");
            using (var reader = new StreamReader(CSV1.OpenReadStream()))
            {
                var headerLine = await reader.ReadLineAsync();
                var headers = headerLine.Split(','); 

                return Ok(headers);
            }
        }

    }
}
