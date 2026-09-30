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
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Data.SqlClient;
using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;

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
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public OmrProcessingController(
            OmrProcessingService omrService,
            IWebHostEnvironment env,
            ApplicationDbContext dbContext,
            WebSoketHandler webSocketHandler,
            OmrProcessingControlService controlService,
            RecordSave recordSave,
            table_gen recordTable,
            ImgSave imgSave,
            FindCordinationClass FindCordinationClass,
            IConfiguration configuration,
            ILogger<OmrProcessingController> logger)
            {
            _omrService = omrService;
            _env = env;
            _dbContext = dbContext;
            _recordTable = recordTable;
            _webSocketHandler = webSocketHandler;
            _controlService = controlService;
            _SaveOnly = recordSave;
            _imgSave = imgSave;
            _configuration = configuration;
            _logger = logger;
            _FindCordinationClass = FindCordinationClass;
                if (controlService == null)
                {
                    throw new ArgumentNullException(nameof(controlService), "OmrProcessingControlService is not injected properly.");
                }
            }

    
        // Process OMR Sheet    
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(string folderPath, string token, int idTemp, bool IsSaveDb, bool failReScan = true)
        {
            dynamic resp;
            _controlService.ResetProcessing();

            try
            {
                _logger.LogInformation("OMR processing started for FolderPath: {FolderPath}, TemplateId: {IdTemp}", folderPath, idTemp);

                // Token handler UserId Extract
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
                var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;
                var folderPAth = folderPath;

                _logger.LogInformation("Processing initiated by UserId: {UserId}, UserName: {UserName}", userId, userName);

                // Y/N = ReScan failure Img Folder.
                var sharefolder = failReScan
                    ? Path.Combine("wFileManager/" + folderPath)
                    : Path.Combine("RejectImg/" + folderPath);

                // Exist path
                folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager/" + folderPath);

                if (!Directory.Exists(folderPath))
                {
                    _logger.LogWarning("Invalid folder path requested: {FolderPath}", folderPath);
                    resp = new
                    {
                        state = false,
                        message = "Folder path is invalid"
                    };
                }
                else
                {
                    var imageFiles = Directory.GetFiles(folderPath, "*.*")
                        .Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg") || f.EndsWith(".tif"))
                        .ToList();

                    var Targetjson = string.Empty;
                    var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == idTemp);

                    if (ReturnDetails == null)
                    {
                        _logger.LogWarning("Template not found in DB for TemplateId: {IdTemp}", idTemp);
                        resp = new
                        {
                            state = false,
                            message = "Id is invalid please add Template first"
                        };
                    }
                    else if (string.IsNullOrEmpty(ReturnDetails.JsonPath))
                    {
                        _logger.LogWarning("JsonPath missing for TemplateId: {IdTemp}", idTemp);
                        resp = new
                        {
                            state = false,
                            message = "Template not found"
                        };
                    }
                    else
                    {
                        string imageUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ReturnDetails.imgPath);
                        string templateName = ReturnDetails.FileName;

                        Targetjson = ReturnDetails.JsonPath.Replace("\\", "/");
                        string templatePath = Path.Combine(_env.WebRootPath, Targetjson);
                        var results = new List<OmrResult>();
                        var crttb = 1;
                        var totalCount = JsonSerializer.Serialize(imageFiles.Count);

                        if (imageFiles.Count == 0)
                        {
                            _logger.LogWarning("No valid image files found in folder: {FolderPath}", folderPath);
                            resp = new
                            {
                                state = false,
                                message = "Image is not found"
                            };
                        }
                        else
                        {
                            _logger.LogInformation("Total {Count} images found for processing", imageFiles.Count);

                            int ser = 0;
                            foreach (var imagePath in imageFiles)
                            {
                                ser++;
                                _logger.LogInformation("Processing image {Index}/{Total}: {ImagePath}", ser, imageFiles.Count, imagePath);

                                // Stop and Continue API Global 
                                _controlService.WaitIfPaused();

                                // Stop handle
                                if (_controlService.IsStopRequested)
                                {
                                    _logger.LogWarning("OMR processing stopped by user request at image index {Index}", ser);
                                    break;
                                }

                                // Scanning to get data from OMR Sheet
                                string ResizedImagePath = SizedMatchedClass.ResizeImageToTemplateSize(imagePath, imageUrl);
                                Console.WriteLine(ResizedImagePath);

                                var res = await _omrService.ProcessOmrSheet(ResizedImagePath, templatePath, imageUrl, ser, userName);
                                OmrProcessingService.MaybeCleanupBatch(25);   // Forcefully Clean batch files
                                results.Add(res);

                                if (crttb == 1)
                                {
                                    await _recordTable.TableCreation(res, userId, folderPAth, userId, idTemp);
                                }
                                crttb++;

                                // 1. Save_Record into DB
                                dynamic dbRes = await _SaveOnly.RecordSaveVal(res, idTemp, userName, userId, IsSaveDb, folderPAth, imagePath, templateName, idTemp);

                                if (IsSaveDb)
                                {
                                    // 2. Save_Scanned Img Folder
                                    var stat = res.Success;
                                    var SaveRoot = await _imgSave.ScanedSave(Directory.GetCurrentDirectory(), imagePath, idTemp, stat, folderPath, userId);
                                }

                                // 3. WS_Handler
                                string jsonResult = JsonSerializer.Serialize(dbRes);
                                userId = Convert.ToString(userId);
                                await _webSocketHandler.UserMessageAsync(userId, jsonResult);
                            }

                            await _webSocketHandler.UserMessageAsync(userId, totalCount);
                            await _webSocketHandler.UserMessageAsync(userId, "");

                            // Download CSV 
                            var jsonString = JsonSerializer.Serialize(results);
                            var csvBytes = Encoding.UTF8.GetBytes(jsonString);

                            _logger.LogInformation("OMR Batch Processing Completed Successfully for UserId: {UserId}", userId);

                            resp = new
                            {
                                state = true,
                                record = results,
                                csv = csvBytes
                            };
                        }
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during OMR processing for FolderPath: {FolderPath}", folderPath);

                return StatusCode(500, new
                {
                    state = false,
                    message = "An internal server error occurred while processing the OMR sheets.",
                    error = ex.Message
                });
            }
        }

        [RequestSizeLimit(3_221_225_472)]
        [RequestFormLimits(MultipartBodyLengthLimit = 3_221_225_472,ValueLengthLimit = int.MaxValue,ValueCountLimit = int.MaxValue)]
        [HttpPost("process-omr2")]
        public async Task<IActionResult> ProcessOmrSheet2(List<IFormFile> images, string folderPath, string token, int idTemp, bool IsSaveDb, bool failReScan = true)
        {
            dynamic resp;
            _controlService.ResetProcessing();

            // Token handler UserId Extract
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
            var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;
            var folderPAth = folderPath;

            // Working directory where uploaded images will be saved physically
            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager", folderPath);
            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var imageFiles = new List<string>();
            if (images == null || images.Count == 0)
            {
                resp = new
                {
                    state = false,
                    message = "No Files selected. Please select at least one image."
                };
                return NotFound(resp);
            }

            //var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".tif" };
            //int ser = 0;
            //var totalCount = JsonSerializer.Serialize(images.Count);
            //var results = new List<OmrResult>();
            //foreach (var file in images)
            //{
            //    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            //    if (!allowedExt.Contains(ext) || file.Length == 0)
            //    {
            //        continue; // skip invalid files
            //    }

            //    var safeFileName = $"{file.FileName}";                 // avoid name clashes; use file.FileName if you want to preserve original names
            //    var savedPath = Path.Combine(uploadFolder, safeFileName);
            //    using (var stream = new FileStream(savedPath, FileMode.Create))
            //    {
            //        await file.CopyToAsync(stream);
            //    }

            //    //imageFiles.Add(savedPath);
            //    var Targetjson = string.Empty;
            //    var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == idTemp);
            //    if (ReturnDetails == null)
            //    {
            //        resp = new
            //        {
            //            state = false,
            //            message = "Id is invalid please add Template first"
            //        };
            //        return NotFound(resp);
            //    }

            //    string imageUrl = ReturnDetails.imgPath;
            //    string templateName = ReturnDetails.FileName;
            //    imageUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl);

            //    if (string.IsNullOrEmpty(ReturnDetails.JsonPath))
            //    {
            //        resp = new
            //        {
            //            state = false,
            //            message = "Template not found"
            //        };
            //        return NotFound(resp);
            //    }

            //    Targetjson = ReturnDetails.JsonPath.Replace("\\", "/");
            //    string templatePath = Path.Combine(_env.WebRootPath, Targetjson);


            //    var crttb = 1;

            //    ser = ser + 1;
            //    _controlService.WaitIfPaused();
            //    if (_controlService.IsStopRequested)
            //    {
            //        break;
            //    }

            //    var res = await _omrService.ProcessOmrSheet(savedPath, templatePath, imageUrl, ser, userName);
            //    results.Add(res);


            //    if (crttb == 1)
            //    {
            //        await _recordTable.TableCreation(res, userId, folderPAth, userId, idTemp);
            //    }
            //    crttb++;

            //    // 1. Save_Record into DB
            //    dynamic dbRes = await _SaveOnly.RecordSaveVal(res, idTemp, userName, userId, IsSaveDb, folderPAth, savedPath, templateName, idTemp);

            //    if (IsSaveDb)
            //    {
            //        // 2. Save_Sacanned Img Folder
            //        var stat = res.Success;
            //        var SaveRoot = await _imgSave.ScanedSave(Directory.GetCurrentDirectory(), savedPath, idTemp, stat, folderPath, userId);
            //    }

            //    // 3. WS_Handler
            //    string jsonResult = JsonSerializer.Serialize(dbRes);
            //    userId = Convert.ToString(userId);
            //    await _webSocketHandler.UserMessageAsync(userId, jsonResult);
            //    if (System.IO.File.Exists(savedPath))
            //    {
            //        System.IO.File.Delete(savedPath);
            //    }
            //}

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".tif" };
            int ser = 0;
            var totalCount = JsonSerializer.Serialize(images.Count);
            var results = new List<OmrResult>();

            foreach (var file in images)
            {
                // Strip out any folder paths sent by the browser (e.g., "Folder/image.jpg" becomes "image.jpg")
                var cleanFileName = Path.GetFileName(file.FileName);
                var ext = Path.GetExtension(cleanFileName).ToLowerInvariant();

                if (!allowedExt.Contains(ext) || file.Length == 0)
                {
                    continue; // skip invalid files
                }

                // Use clean file name and ensure no double extension
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(cleanFileName);
                var safeFileName = $"{fileNameWithoutExt}{ext}";
                var savedPath = Path.Combine(uploadFolder, safeFileName);

                // Ensure the target directory physically exists (handles nested paths safely)
                var fileDirectory = Path.GetDirectoryName(savedPath);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }

                using (var stream = new FileStream(savedPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Rest of your processing logic...
                var Targetjson = string.Empty;
                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == idTemp);
                if (ReturnDetails == null)
                {
                    resp = new { state = false, message = "Id is invalid please add Template first" };
                    return NotFound(resp);
                }

                string imageUrl = ReturnDetails.imgPath;
                string templateName = ReturnDetails.FileName;
                imageUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl);

                if (string.IsNullOrEmpty(ReturnDetails.JsonPath))
                {
                    resp = new { state = false, message = "Template not found" };
                    return NotFound(resp);
                }

                Targetjson = ReturnDetails.JsonPath.Replace("\\", "/");
                string templatePath = Path.Combine(_env.WebRootPath, Targetjson);

                var crttb = 1;
                ser = ser + 1;
                _controlService.WaitIfPaused();
                if (_controlService.IsStopRequested)
                {
                    break;
                }

                var res = await _omrService.ProcessOmrSheet(savedPath, templatePath, imageUrl, ser, userName);
                results.Add(res);

                if (crttb == 1)
                {
                    await _recordTable.TableCreation(res, userId, folderPAth, userId, idTemp);
                }
                crttb++;

                dynamic dbRes = await _SaveOnly.RecordSaveVal(res, idTemp, userName, userId, IsSaveDb, folderPAth, savedPath, templateName, idTemp);

                string updatepath = "";
                if (IsSaveDb)
                {
                    var stat = res.Success;
                    var SaveRoot = await _imgSave.ScanedSave(Directory.GetCurrentDirectory(), savedPath, idTemp, stat, folderPath, userId);
                    Console.WriteLine(SaveRoot);

                    var jsonResult1 = SaveRoot as JsonResult;
                    dynamic resultValue = jsonResult1.Value;
                    var imagePath = resultValue.imagepath;
                    updatepath = imagePath.Substring(imagePath.IndexOf(@"wFileManager", StringComparison.OrdinalIgnoreCase)).Replace("/", "\\");
                }


                foreach (var data in dbRes)
                {
                    if (data.Key == "FileName")
                    {
                        dbRes[data.Key] = updatepath;
                    }
                }


                string jsonResult = JsonSerializer.Serialize(dbRes);
                Console.WriteLine(jsonResult);
                userId = Convert.ToString(userId);
                await _webSocketHandler.UserMessageAsync(userId, jsonResult);
                try
                {
                    if (System.IO.File.Exists(savedPath))
                    {
                        System.IO.File.Delete(savedPath);
                    }
                }
                catch (IOException)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    try
                    {
                        if (System.IO.File.Exists(savedPath))
                        {
                            System.IO.File.Delete(savedPath);
                        }
                    }
                    catch
                    {
                        // Ignore or log if it still fails; background cleanup tasks can handle it later if needed
                    }
                }
            }

            // 
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

            return Ok(resp);
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

        [HttpGet("DataResponce")]
        public async Task<IActionResult> DataResponce( int PageNo, int PageSize, string tempId, string folderName)
        {
            dynamic res;
            dynamic dataResp = "";
            int value = 0;
            try
            {
                var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;

                using (var _conn = new SqlConnection(_configuration.GetConnectionString("dbc")))
                {
                    _conn.Open();
                    string checkTableSql = $@"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'Tem_{userId}_${tempId}$_{folderName}_%'";
                    var exists = _conn.QueryFirstOrDefault(checkTableSql);
                    if (exists != null)
                    {
                        string querry = $"SELECT * FROM [{exists.TABLE_NAME}] ORDER BY Id  OFFSET ({PageNo} - 1) * {PageSize} ROWS FETCH NEXT {PageSize} ROWS ONLY"; 
                        dataResp = _conn.Query(querry);


                        value = _conn.QuerySingle<int>($"SELECT count(*) FROM [{exists.TABLE_NAME}]");

                    }
                    else
                    {
                        dataResp = "Not Found Record";
                    }
                }
                res = new
                {
                    status = true,
                    Record = dataResp,
                    Total = value
                };
            }
            catch (Exception ex)
            {
                res = new
                {
                    status = false,
                    Messages = ex.Message
                };
            }
            return Ok(res);
        }


        [RequestSizeLimit(3_221_225_472)]
        [RequestFormLimits(MultipartBodyLengthLimit = 3_221_225_472, ValueLengthLimit = int.MaxValue, ValueCountLimit = int.MaxValue)]
        [HttpPost("mobileImageQC")]
        public async Task<IActionResult> mobileImageQC(List <IFormFile> InsertImage)
        {
            dynamic res;
            try {

       
            }
            catch(Exception ex) {
                
            }
            return Ok();
        }

    }
}
