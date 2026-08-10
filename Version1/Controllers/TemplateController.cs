using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Syncfusion.EJ2.Notifications;
using Version1.Data;
using Version1.Modal;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenCvSharp.XImgProc.CvXImgProc;

namespace SQCScanner.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    [ApiController]
    public class TemplateController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger _logger;
        public TemplateController(IWebHostEnvironment env, ApplicationDbContext dbContext, ILogger<TemplateController> logger)
        {
            _env = env;
            _dbContext = dbContext;
            _logger = logger;
        }

        // DONE - - yh 
        //[HttpPost]
        //[Route("Create_ImeTemp")]
        //public async Task<IActionResult> imgRec(IFormFile ImgTemp, string TempName, string empId, string? description)
        //{
        //    dynamic res;
        //    bool exist = true;

        //    if (ImgTemp == null || ImgTemp.Length == 0)
        //    {
        //        res = new
        //        {
        //            state = false,
        //            message = "No file uploaded."
        //        };
        //    }
        //    else
        //    {
        //        string extension = Path.GetExtension(ImgTemp.FileName).ToLower();
        //        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
        //        {
        //            res = new
        //            {
        //                state = false,
        //                message = "Invalid file type. Only .jpg, .jpeg, or .png are allowed."
        //            };
        //        }
        //        else
        //        {
        //            try
        //            {
        //                description = description ?? "";
        //                string fileName = Path.GetFileName(ImgTemp.FileName);
        //                var TempNameUnq = _dbContext.ImgTemplate.Select(x => x.FileName).ToList();
        //                foreach (var tempUnq in TempNameUnq)
        //                {
        //                    if (tempUnq == TempName)
        //                    {
        //                        exist = false;
        //                    }
        //                }
        //                if (exist == true)
        //                {
        //                    string uploadsFolder = Path.Combine(_env.WebRootPath, "ImageManager");
        //                    if (!Directory.Exists(uploadsFolder))
        //                    {
        //                        Directory.CreateDirectory(uploadsFolder);
        //                    }
        //                    var ImgfileName = Path.GetFileName(ImgTemp.FileName);
        //                    string filePath = Path.Combine(uploadsFolder, ImgfileName);   //  $"{Guid.NewGuid(ImgfileName)}
        //                    string relativeImgPath = Path.Combine("ImageManager", ImgfileName).Replace("\\", "/");
        //                    var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == fileName);
        //                    var results = new List<ImgTemp>();
        //                    if (!exists)
        //                    {
        //                        using (var TempSet = new FileStream(filePath, FileMode.Create))
        //                        {
        //                            await ImgTemp.CopyToAsync(TempSet);
        //                        }
        //                        // Save into DB
        //                        DateTime Date = DateTime.Now;
        //                        string toDay = Date.ToString();
        //                        string fileNameMix = $"{TempName}##{empId}";
        //                        var resp = _dbContext.Add(new ImgTemp
        //                        {
        //                            FileName = fileNameMix,
        //                            imgPath = relativeImgPath,
        //                            JsonPath = "",
        //                            CreateAt = toDay,
        //                            discription = description
        //                        });
        //                        await _dbContext.SaveChangesAsync();
        //                        results.Add(resp.Entity);
        //                        res = new
        //                        {
        //                            message = "File Save into Table and Folder",
        //                            status = true,
        //                            data = results
        //                        };
        //                    }
        //                    else
        //                    {
        //                        res = new
        //                        {
        //                            message = "File Not Exist",
        //                            status = false,
        //                        };
        //                    }
        //                }
        //                else
        //                {
        //                    res = new
        //                    {
        //                        state = false,
        //                        message = "FileName is Already Exist"
        //                    };
        //                }

        //            }
        //            catch (Exception ex)
        //            {
        //                res = new
        //                {
        //                    message = ex.Message,
        //                    status = false,
        //                };
        //            }
        //        }
        //    }
        //    bool currentState = res.status;
        //    if (currentState)
        //    {
        //        return Ok(res);
        //    }
        //    else
        //    {
        //        return NotFound(res);
        //    }
        //}

        [HttpPost]
        [Route("Create_ImeTemp")]
        public async Task<IActionResult> imgRec(IFormFile ImgTemp, string TempName, string empId, string? description)
        {
            dynamic res;
            bool exist = true;

            _logger.LogInformation("Create_ImeTemp API called. TempName: {TempName}, EmpId: {EmpId}", TempName, empId);

            if (ImgTemp == null || ImgTemp.Length == 0)
            {
                _logger.LogWarning("No file uploaded. TempName: {TempName}", TempName);

                res = new
                {
                    state = false,
                    message = "No file uploaded."
                };
            }
            else
            {
                string extension = Path.GetExtension(ImgTemp.FileName).ToLower();

                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    _logger.LogWarning("Invalid file type uploaded: {FileName}", ImgTemp.FileName);

                    res = new
                    {
                        state = false,
                        message = "Invalid file type. Only .jpg, .jpeg, or .png are allowed."
                    };
                }
                else
                {
                    try
                    {
                        description ??= "";

                        _logger.LogInformation("Image validation completed for {FileName}", ImgTemp.FileName);

                        string fileName = Path.GetFileName(ImgTemp.FileName);

                        var TempNameUnq = _dbContext.ImgTemplate
                                                    .Select(x => x.FileName)
                                                    .ToList();

                        foreach (var tempUnq in TempNameUnq)
                        {
                            if (tempUnq == TempName)
                            {
                                exist = false;
                                break;
                            }
                        }

                        if (exist)
                        {
                            string uploadsFolder = Path.Combine(_env.WebRootPath, "ImageManager");

                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);

                                _logger.LogInformation("ImageManager folder created.");
                            }

                            string ImgfileName = Path.GetFileName(ImgTemp.FileName);

                            string filePath = Path.Combine(uploadsFolder, ImgfileName);

                            string relativeImgPath = Path.Combine("ImageManager", ImgfileName)
                                .Replace("\\", "/");

                            var fileExists = await _dbContext.ImgTemplate
                                .AnyAsync(t => t.FileName == fileName);

                            var results = new List<ImgTemp>();

                            if (!fileExists)
                            {
                                using (var fs = new FileStream(filePath, FileMode.Create))
                                {
                                    await ImgTemp.CopyToAsync(fs);
                                }

                                _logger.LogInformation("Image saved successfully at {Path}", filePath);

                                DateTime date = DateTime.Now;
                                string fileNameMix = $"{TempName}##{empId}";

                                var resp = _dbContext.Add(new ImgTemp
                                {
                                    FileName = fileNameMix,
                                    imgPath = relativeImgPath,
                                    JsonPath = "",
                                    CreateAt = date.ToString(),
                                    discription = description
                                });

                                await _dbContext.SaveChangesAsync();

                                _logger.LogInformation(
                                    "Database record created successfully. TempName: {TempName}, Employee: {EmpId}",
                                    TempName,
                                    empId);

                                results.Add(resp.Entity);

                                res = new
                                {
                                    message = "File Save into Table and Folder",
                                    status = true,
                                    data = results
                                };
                            }
                            else
                            {
                                _logger.LogWarning("File already exists in database. FileName: {FileName}", fileName);

                                res = new
                                {
                                    message = "File Not Exist",
                                    status = false,
                                };
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Template name already exists. TempName: {TempName}", TempName);

                            res = new
                            {
                                state = false,
                                message = "FileName is Already Exist"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Exception occurred while uploading image. TempName: {TempName}, EmpId: {EmpId}",
                            TempName,
                            empId);

                        res = new
                        {
                            message = ex.Message,
                            status = false,
                        };
                    }
                }
            }

            bool currentState = res.status;

            if (currentState)
            {
                _logger.LogInformation("Create_ImeTemp API completed successfully.");
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Create_ImeTemp API failed.");
                return NotFound(res);
            }
        }









        // DONE - -
        [HttpGet]
        [Route("Single_ImeTem")]
        public async Task<IActionResult> getImgTemp(int id)
        {
            dynamic res;
            try
            {
                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == id);
                if (ReturnDetails != null)
                {
                    if (!string.IsNullOrEmpty(ReturnDetails.imgPath))
                    {
                        ReturnDetails.imgPath = ReturnDetails.imgPath.Replace("\\", "/");
                    }
                    if(!string.IsNullOrEmpty(ReturnDetails.JsonPath))
                    {
                        ReturnDetails.JsonPath = ReturnDetails.JsonPath.Replace("\\", "/");
                    }
                    res = new
                    {
                        data = ReturnDetails,
                        state = true,
                        Message = "Record Found"
                    };
                }
                else
                {
                    res = new
                    {
                        state = false,
                        Message = "Record Not Found",
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            bool currentStatex = res.state;
            if (currentStatex)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        // DONE - -
        [HttpGet]
        [Route("List_ImeTemp")]
        public async Task<IActionResult> getImgTemp()
        {
            dynamic res;
            try
            {
                var results = await _dbContext.ImgTemplate.ToListAsync();
                if (results.Count == null || results.Count == 0)
                {
                    res = new
                    {
                        state = false,
                        message = "Record Not Found"
                    };
                }
                else
                {
                    foreach (var item in results)
                    {
                        if (!string.IsNullOrEmpty(item.imgPath))
                        {
                            item.imgPath = item.imgPath.Replace("\\", "/");
                        }
                        if (!string.IsNullOrEmpty(item.JsonPath))
                        {
                            item.JsonPath = item.JsonPath.Replace("\\", "/");
                        }
                    }
                    res = new
                    {
                        state = true,
                        message = "Record Found",
                        body = results
                    };
                }
            }catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
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

        // File will be deleted from DB and File Manager too
        //[HttpDelete]
        //[Route("Del_ImeTemp")]
        //public async Task<IActionResult> deleteTemp(int id)
        //{
        //    dynamic res;
        //    try
        //    {
        //        // Remove perticular id_Data from DB
        //        var tempRecord = await _dbContext.ImgTemplate.FindAsync(id);
        //        if (tempRecord == null)
        //        {
        //            res = new
        //            {
        //                state = false,
        //                message = "Template not found."
        //            };
        //        }
        //        else
        //        {
        //            _dbContext.ImgTemplate.Remove(tempRecord);
        //            await _dbContext.SaveChangesAsync();

        //            // Img Del  -- Root folder
        //            string ImgFold = Path.Combine(_env.WebRootPath, "ImageManager");
        //            var imgTemp = tempRecord.imgPath;
        //            string ImgFileName = Path.GetFileName(imgTemp);
        //            string ImgFilePath = Path.Combine(ImgFold, ImgFileName);
        //            if (System.IO.File.Exists(ImgFilePath))
        //            {
        //                await Task.Run(() => System.IO.File.Delete(ImgFilePath));
        //            }
        //            if (!string.IsNullOrEmpty(tempRecord?.JsonPath))
        //            {
        //                string TempFold = Path.Combine(_env.WebRootPath, "TempManager");
        //                var JsonTemp = tempRecord.JsonPath;
        //                string TempFileName = Path.GetFileName(JsonTemp);
        //                string JsonFilePath = Path.Combine(TempFold, TempFileName);
        //                if (System.IO.File.Exists(JsonFilePath))
        //                {
        //                    await Task.Run(() => System.IO.File.Delete(JsonFilePath));
        //                }
        //            }
        //            res = new
        //            {
        //                message = "Template delete from database and fileManager.",
        //                state = true
        //            };
        //        }
        //    }
        //    catch(Exception ex)
        //    {
        //        res = new
        //        {
        //            message = ex.Message,
        //            state = false
        //        };
        //    }
        //    bool currentState = res.state;
        //    if (currentState)
        //    {
        //        return Ok(res);
        //    }
        //    else
        //    {
        //        return NotFound(res);
        //    }
        //}

        [HttpDelete]
        [Route("Del_ImeTemp")]
        public async Task<IActionResult> deleteTemp(int id)
        {
            dynamic res;

            _logger.LogInformation("Delete template request received. TemplateId: {TemplateId}", id);

            try
            {
                // Find record
                var tempRecord = await _dbContext.ImgTemplate.FindAsync(id);

                if (tempRecord == null)
                {
                    _logger.LogWarning("Template not found. TemplateId: {TemplateId}", id);

                    res = new
                    {
                        state = false,
                        message = "Template not found."
                    };
                }
                else
                {
                    _logger.LogInformation(
                        "Deleting template. TemplateId: {TemplateId}, FileName: {FileName}",
                        id,
                        tempRecord.FileName);

                    // Delete DB Record
                    _dbContext.ImgTemplate.Remove(tempRecord);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Database record deleted successfully. TemplateId: {TemplateId}", id);

                    // Delete Image
                    string imgFolder = Path.Combine(_env.WebRootPath, "ImageManager");
                    string imgFileName = Path.GetFileName(tempRecord.imgPath);
                    string imgFilePath = Path.Combine(imgFolder, imgFileName);

                    if (System.IO.File.Exists(imgFilePath))
                    {
                        await Task.Run(() => System.IO.File.Delete(imgFilePath));

                        _logger.LogInformation("Image deleted successfully. Path: {ImagePath}", imgFilePath);
                    }
                    else
                    {
                        _logger.LogWarning("Image file not found. Path: {ImagePath}", imgFilePath);
                    }

                    // Delete JSON
                    if (!string.IsNullOrEmpty(tempRecord.JsonPath))
                    {
                        string tempFolder = Path.Combine(_env.WebRootPath, "TempManager");
                        string jsonFileName = Path.GetFileName(tempRecord.JsonPath);
                        string jsonFilePath = Path.Combine(tempFolder, jsonFileName);

                        if (System.IO.File.Exists(jsonFilePath))
                        {
                            await Task.Run(() => System.IO.File.Delete(jsonFilePath));

                            _logger.LogInformation("JSON file deleted successfully. Path: {JsonPath}", jsonFilePath);
                        }
                        else
                        {
                            _logger.LogWarning("JSON file not found. Path: {JsonPath}", jsonFilePath);
                        }
                    }

                    res = new
                    {
                        message = "Template delete from database and fileManager.",
                        state = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception occurred while deleting template. TemplateId: {TemplateId}",
                    id);

                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }

            if (res.state)
            {
                _logger.LogInformation("Delete template completed successfully. TemplateId: {TemplateId}", id);
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Delete template failed. TemplateId: {TemplateId}", id);
                return NotFound(res);
            }
        }

        // As per File Name temp Url will be set into db along with save into fileManager
        //[HttpPut]
        //[Route("Update_ImeTemp")]
        //public async Task<IActionResult> Update(string FileName, IFormFile tempName) 
        //{
        //    dynamic res;
        //    try
        //    {
        //        if (tempName != null && Path.GetExtension(tempName.FileName).ToLower() == ".json")
        //        {

        //            // save new JSON file into Folder
        //            string uploadsFolder = Path.Combine(_env.WebRootPath, "TempManager");

        //            var fileName = Path.GetFileName(tempName.FileName);
        //            string filePath = Path.Combine(uploadsFolder, fileName);
        //            string relativePath = Path.Combine("TempManager", fileName).Replace("\\", "/");

        //            var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == FileName);
        //            if (exists)
        //            {
        //                // DB table is update -- DONE
        //                DateTime currentDate = DateTime.Now;
        //                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.FileName == FileName);
        //                var fileNameDel = ReturnDetails.JsonPath;    // update
        //                ReturnDetails.JsonPath = relativePath;
        //                ReturnDetails.CreateAt = currentDate.ToString();
        //                await _dbContext.SaveChangesAsync();

        //                // delete old File 
        //                var delOldRelative = fileNameDel;
        //                var delOldFullPath = Path.Combine(_env.WebRootPath, delOldRelative.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
        //                if (System.IO.File.Exists(delOldFullPath))
        //                {
        //                    System.IO.File.Delete(delOldFullPath);
        //                }

        //                // save into root folder
        //                using (var TempSet = new FileStream(filePath, FileMode.Create))
        //                {
        //                    await tempName.CopyToAsync(TempSet);
        //                }

        //                res = new
        //                {
        //                    message = "JSON Template JSON SAVE",
        //                    state = true
        //                };
        //            }
        //            else
        //            {
        //                res = new
        //                {
        //                    message = "Template not found in database.",
        //                    state = false
        //                };
        //            }
        //        }
        //        else
        //        {
        //            res = new
        //            {
        //                message = "Template not JSON.",
        //                state = false
        //            };
        //        }
        //    }catch(Exception ex)
        //    {
        //        res = new
        //        {
        //            message = ex.Message,
        //            state = false,
        //        };
        //    }
        //    bool currentState = res.state;
        //    if (currentState)
        //    {
        //        return Ok(res);
        //    }
        //    else
        //    {
        //        return NotFound(res);
        //    }
        //}

        [HttpPut]
        [Route("Update_ImeTemp")]
        public async Task<IActionResult> Update(string FileName, IFormFile tempName)
        {
            dynamic res;

            _logger.LogInformation("Update_ImeTemp API called. Template: {FileName}", FileName);

            try
            {
                if (tempName != null && Path.GetExtension(tempName.FileName).ToLower() == ".json")
                {
                    _logger.LogInformation("JSON file validation passed. Uploaded File: {UploadedFile}", tempName.FileName);

                    // Save new JSON file into Folder
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "TempManager");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                        _logger.LogInformation("TempManager folder created.");
                    }

                    var fileName = Path.GetFileName(tempName.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);
                    string relativePath = Path.Combine("TempManager", fileName).Replace("\\", "/");

                    var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == FileName);

                    if (exists)
                    {
                        var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.FileName == FileName);

                        var oldJsonPath = ReturnDetails.JsonPath;

                        ReturnDetails.JsonPath = relativePath;
                        ReturnDetails.CreateAt = DateTime.Now.ToString();

                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation("Database updated successfully for Template: {FileName}", FileName);

                        // Delete old JSON
                        var oldFullPath = Path.Combine(
                            _env.WebRootPath,
                            oldJsonPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                        if (System.IO.File.Exists(oldFullPath))
                        {
                            System.IO.File.Delete(oldFullPath);

                            _logger.LogInformation("Old JSON deleted: {OldFile}", oldJsonPath);
                        }
                        else
                        {
                            _logger.LogWarning("Old JSON file not found: {OldFile}", oldJsonPath);
                        }

                        // Save New JSON
                        using (var TempSet = new FileStream(filePath, FileMode.Create))
                        {
                            await tempName.CopyToAsync(TempSet);
                        }

                        _logger.LogInformation("New JSON saved successfully at {Path}", filePath);

                        res = new
                        {
                            message = "JSON Template SAVE",
                            state = true
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Template not found in database. FileName: {FileName}", FileName);

                        res = new
                        {
                            message = "Template not found in database.",
                            state = false
                        };
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid file uploaded. Only JSON files are allowed.");

                    res = new
                    {
                        message = "Template not JSON.",
                        state = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error while updating template. FileName: {FileName}",
                    FileName);

                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }

            if (res.state)
            {
                _logger.LogInformation("Update_ImeTemp completed successfully. Template: {FileName}", FileName);
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Update_ImeTemp failed. Template: {FileName}", FileName);
                return NotFound(res);
            }
        }

    }
}
