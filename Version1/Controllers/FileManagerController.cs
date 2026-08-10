using DocumentFormat.OpenXml.Math;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Syncfusion.EJ2.FileManager.Base;
using Syncfusion.EJ2.FileManager.PhysicalFileProvider;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Syncfusion.EJ2.Notifications;
using Org.BouncyCastle.Ocsp;
using OpenCvSharp.Aruco;
using System.IO;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using OpenCvSharp;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    //[Authorize]
    public class FileManagerController : Controller
    {

        private readonly Syncfusion.EJ2.FileManager.PhysicalFileProvider.PhysicalFileProvider _operation;
        private readonly string _root = Environment.CurrentDirectory + "\\wFileManager";
        public FileManagerController(IWebHostEnvironment hostingEnvironment)
        {
            _operation = new Syncfusion.EJ2.FileManager.PhysicalFileProvider.PhysicalFileProvider();
            _operation.RootFolder(Path.Combine(_root));
        }

        [HttpGet("folders")]
        public IActionResult GetFolders([FromQuery] string path)
        {
            try
            {
                var fullPath = Path.Combine(_root, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                {
                    return NotFound("Directory not found");
                }

                var directories = Directory.GetDirectories(fullPath)
                    .Select(d => new { Name = Path.GetFileName(d), FullPath = d })
                    .ToList();

                return Ok(directories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("folders")]
        public IActionResult CreateFolder([FromQuery] string path, [FromQuery] string folderName)
        {
            try
            {
                var fullPath = Path.Combine(_root, path ?? string.Empty, folderName);
                if (Directory.Exists(fullPath))
                {
                    return BadRequest("Directory already exists");
                }

                Directory.CreateDirectory(fullPath);
                return Ok("Directory created successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("FileOperations")]
        public IActionResult FileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Action == "delete" || args.Action == "rename")
            {
                if (args.TargetPath == null && string.IsNullOrEmpty(args.Path))
                {
                    var response = new FileManagerResponse
                    {
                        Error = new ErrorDetails { Code = "401", Message = "Restricted to modify the root folder." }
                    };
                    return StatusCode(401, response);
                }
            }

            switch (args.Action)
            {
                case "read":
                    return Ok(_operation.ToCamelCase(_operation.GetFiles(args.Path, args.ShowHiddenItems)));
                case "delete":
                    return Ok(_operation.ToCamelCase(_operation.Delete(args.Path, args.Names)));
                case "copy":
                    return Ok(_operation.ToCamelCase(_operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData)));
                case "move":
                    return Ok(_operation.ToCamelCase(_operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData)));
                case "details":
                    return Ok(_operation.ToCamelCase(_operation.Details(args.Path, args.Names, args.Data)));
                case "create":
                    return Ok(_operation.ToCamelCase(_operation.Create(args.Path, args.Name)));
                case "search":
                    return Ok(_operation.ToCamelCase(_operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive)));
                case "rename":
                    return Ok(_operation.ToCamelCase(_operation.Rename(args.Path, args.Name, args.NewName, false, args.ShowFileExtension, args.Data)));
                default:
                    return BadRequest("Invalid action.");
            }
        }

        [HttpPost]
        [Route("Upload")]
        public IActionResult Upload([FromForm] string path, [FromForm] IList<IFormFile> uploadFiles, [FromForm] string action)
        {
            try
            {
                foreach (var file in uploadFiles)
                {
                    var folders = file.FileName.Split('/');
                    if (folders.Length > 1)
                    {
                        for (var i = 0; i < folders.Length - 1; i++)
                        {
                            string newDirectoryPath = Path.Combine(_root + path, folders[i]);
                            if (Path.GetFullPath(newDirectoryPath) != Path.GetDirectoryName(newDirectoryPath) + Path.DirectorySeparatorChar + folders[i])
                            {
                                throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                            }
                            if (!Directory.Exists(newDirectoryPath))
                            {
                                _operation.Create(path, folders[i]);
                            }
                            path += folders[i] + "/";
                        }
                    }
                }

                var uploadResponse = _operation.Upload(path, uploadFiles, action);
                if (uploadResponse.Error != null)
                {
                    return StatusCode(Convert.ToInt32(uploadResponse.Error.Code), uploadResponse);
                }

                return Ok(uploadResponse);
            }
            catch (Exception e)
            {
                var errorResponse = new ErrorDetails
                {
                    Message = "Access denied for Directory-traversal",
                    Code = "417"
                };
                return StatusCode(Convert.ToInt32(errorResponse.Code), errorResponse);
            }
        }

        [Route("Download")]
        [HttpPost]
        public IActionResult Download([FromForm] string downloadInput)
        {
            var args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return _operation.Download(args.Path, args.Names, args.Data);
        }

        [Route("GetImage")]
        [HttpGet]
        public IActionResult GetImage([FromQuery] string path, [FromQuery] string id)
        {
            var args = new FileManagerDirectoryContent
            {
                Path = path,
                Id = id
            };
            return _operation.GetImage(args.Path, args.Id, false, null, null);
        }



        // Mobile VErsion
        [HttpGet("foldersM")]
        public IActionResult GetFoldersM([FromQuery] string path)
        {
            dynamic res;
            try
            {
                var Empid = GetEmpId();
                var fullPath = Path.Combine(_root, Empid, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                {
                    res = new
                    {
                        state = false,
                        message = "Directory not found"
                    };
                }
                else
                {
                    var folders = Directory.GetDirectories(fullPath)
                    .Select(d => new
                    {
                        Type = "Folder",
                        Name = Path.GetFileName(d),
                        FullPath = d
                    });

                    var files = Directory.GetFiles(fullPath)
                        .Select(f => new
                        {
                            Type = "File",
                            Name = Path.GetFileName(f),
                            FullPath = f
                        });

                    var items = folders.Concat(files).ToList();
                    res = new
                    {
                        state = true,
                        data = items
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

        [HttpPost("foldersM")]
        public IActionResult CreateFolderM([FromQuery] string path, [FromQuery] string folderName)
        {
            dynamic res;
            try
            {
                
                var empId = GetEmpId();

                var fullPath = Path.Combine(_root, empId, path ?? string.Empty, folderName);
                
                if (Directory.Exists(fullPath))     
                {
                    res = new
                    {
                        state = false,
                        message = "Directory already exists"
                    };
                }
                else
                {
                    Directory.CreateDirectory(fullPath);
                    res = new
                    {
                        state = true,
                        message = "Directory created successfully"
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = $"Internal server error: {ex.Message}"
                };
            }
            return Ok(res);
        }
        
        [HttpPost]
        [Route("FileOperationsM")]
        public IActionResult FileOperationsM([FromBody] FileManagerDirectoryContent args)
        {
            dynamic res;
            try {
                var empId = GetEmpId();
                if (args.Action == "delete" || args.Action == "rename")
                {
                    if (args.TargetPath == null && string.IsNullOrEmpty(args.Path))
                    {
                        var response = new FileManagerResponse
                        {
                            Error = new ErrorDetails { Code = "401", Message = "Restricted to modify the root folder." }
                        };
                        return StatusCode(401, response);
                    }
                }
                dynamic dataRes;
                switch (args.Action)
                {
                    case "read":
                        return res = new { state = true, dataRes = _operation.ToCamelCase(_operation.GetFiles(args.Path, args.ShowHiddenItems)) };
                    case "delete":
                        return res = new { state = true, dataRes = _operation.ToCamelCase(_operation.Delete(args.Path, args.Names)) };
                    case "copy":
                        return res = new { state = true, dataRes = _operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData)};
                    case "move":
                    return res = new
                    {
                        state = true,
                        dataRes = _operation.ToCamelCase(_operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData))
                    };
                    case "details":
                    return res = new 
                    { 
                        state = true, 
                        dataRes = _operation.ToCamelCase(_operation.Details(args.Path, args.Names, args.Data))
                    };
                    case "create":
                    return res = new
                    {
                        state = true,
                        dataRes = _operation.ToCamelCase(_operation.Create(args.Path, args.Name))
                    };
                    case "search":
                    return res = new { 
                        state = true, 
                        dataRes = _operation.ToCamelCase(_operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive)) 
                    };
                    case "rename":
                    return res = new
                    {
                        state = true,
                        dataRes =  _operation.ToCamelCase(_operation.Rename(args.Path, args.Name, args.NewName, false, args.ShowFileExtension, args.Data))
                    };
                    default:
                    return res = new 
                    {
                        state = false, 
                        message= "Invalid action" 
                    };
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            return Ok(res);
        }


        //[HttpPost]
        //[Route("UploadM")]
        //public IActionResult UploadM([FromForm] string path, [FromForm] IList<IFormFile> uploadFiles, [FromForm] string action)
        //{
        //    dynamic res;
        //    try
        //    {
        //        var empId = GetEmpId();
        //        if (!string.IsNullOrEmpty(path) && !path.StartsWith("/"))
        //        {
        //            path = "/" + path;
        //        }

        //        path = path.TrimStart('/', '\\');
        //        string filePath = Path.Combine( Directory.GetCurrentDirectory(), "wFileManager", path);
        //        if (!Directory.Exists(filePath))
        //        {
        //            Directory.CreateDirectory(filePath);
        //        }

        //        foreach (var file in uploadFiles)
        //        {
        //            Console.WriteLine(file.FileName);
        //            var folders = file.FileName.Split('/');
        //            if (folders.Length >= 1)
        //                {
        //                    var currentPath = path;
        //                    for (var i = 0; i < folders.Length - 1; i++)
        //                    {
        //                        var folderName = folders[i];

        //                        // Reject traversal attempts
        //                        if (string.IsNullOrEmpty(folderName) || folderName == "." || folderName == ".." || folderName.Contains('\\'))
        //                        {
        //                            throw new UnauthorizedAccessException("Access denied for Directory-traversal");
        //                        }

        //                        string newDirectoryPath = Path.Combine(_root, currentPath.TrimStart('/'), folderName);
        //                        var rootFull = Path.GetFullPath(_root) .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        //                        var targetFull = Path.GetFullPath(newDirectoryPath);
        //                        if (!targetFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        //                        {
        //                            throw new UnauthorizedAccessException("Access denied for Directory-traversal");
        //                        }

        //                        if (!Directory.Exists(newDirectoryPath))
        //                        {
        //                            _operation.Create(currentPath, folderName);
        //                        }
        //                        currentPath += folderName + "/";
        //                    }
        //                    path = currentPath;
        //                }
        //                Console.WriteLine("FILE : " + file.FileName);
        //            }

        //        var uploadResponse = _operation.Upload(path, uploadFiles, action);
        //        Console.WriteLine(uploadResponse);

        //        if (uploadResponse.Error != null)
        //        {
        //            res = new
        //            {
        //                state = false,
        //                message = uploadResponse.Error.Message
        //            };
        //            return StatusCode(Convert.ToInt32(uploadResponse.Error.Code), res);
        //        }

        //        res = new
        //        {
        //            state = true,
        //            message = "File uploaded successfully"
        //        };
        //        return Ok(res);
        //    }
        //    catch (Exception e)
        //    {
        //        res = new
        //        {
        //            state = false,
        //            message = "Access denied for Directory-traversal: " + e.Message
        //        };
        //        return StatusCode(417, res);
        //    }
        //}



        // Done
        [HttpPost]
        [Route("UploadM")]
        public IActionResult UploadM([FromForm] string path, [FromForm] IList<IFormFile> uploadFiles, [FromForm] string action)
        {
            try
            {
                var empId = GetEmpId(); // JWT se mila empId
                if (string.IsNullOrWhiteSpace(empId))
                {
                    return Unauthorized(new ErrorDetails { Message = "Invalid or missing token", Code = "401" });
                }

                // client se aaya hua path ignore/sanitize karke empId-scoped path banao
                string empRootRelative = "/" + empId;
                string clientSubPath = (path ?? "/").TrimStart('/');
                string effectivePath = string.IsNullOrEmpty(clientSubPath)
                    ? empRootRelative + "/"
                    : empRootRelative + "/" + clientSubPath;

                if (!effectivePath.EndsWith("/"))
                    effectivePath += "/";

                string empPhysicalRoot = Path.Combine(_root, empId);
                if (!Directory.Exists(empPhysicalRoot))
                {
                    Directory.CreateDirectory(empPhysicalRoot);
                }

                path = effectivePath;

                foreach (var file in uploadFiles)
                {
                    var folders = file.FileName.Split('/');
                    if (folders.Length > 1)
                    {
                        for (var i = 0; i < folders.Length - 1; i++)
                        {
                            string newDirectoryPath = Path.Combine(_root + path, folders[i]);

                            string fullPath = Path.GetFullPath(newDirectoryPath);
                            string expectedRoot = Path.GetFullPath(empPhysicalRoot);
                            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                            }

                            if (!Directory.Exists(newDirectoryPath))
                            {
                                _operation.Create(path, folders[i]);
                            }
                            path += folders[i] + "/";
                        }
                    }
                }

                // ---- Naya point: final target path exist na kare to create kar do, warna skip ----
                string finalTargetPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager", path.TrimStart('/'));

                // yahan bhi traversal-safety check zaroori hai
                string finalFullPath = Path.GetFullPath(finalTargetPath);
                string empRootFullPath = Path.GetFullPath(empPhysicalRoot);
                if (!finalFullPath.StartsWith(empRootFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                }

                if (!Directory.Exists(finalTargetPath))
                {
                    Directory.CreateDirectory(finalTargetPath);
                }
                // agar exist karta hai to kuch nahi karna — seedha upload continue

                var uploadResponse = _operation.Upload(path, uploadFiles, action);
                if (uploadResponse.Error != null)
                {
                    return StatusCode(Convert.ToInt32(uploadResponse.Error.Code), uploadResponse);
                }
                return Ok(uploadResponse);
            }
            catch (Exception e)
            {
                var errorResponse = new ErrorDetails
                {
                    Message = "Access denied for Directory-traversal",
                    Code = "417"
                };
                return StatusCode(Convert.ToInt32(errorResponse.Code), errorResponse);
            }
        }


        [HttpPost]
        [Route("UploadDeleteView")]
        public IActionResult UploadDeleteView([FromForm] string path, [FromForm] IList<IFormFile> uploadFiles, [FromForm] string action)
        {
            try
            {
                var empId = GetEmpId(); // JWT se mila empId
                if (string.IsNullOrWhiteSpace(empId))
                {
                    return Unauthorized(new ErrorDetails { Message = "Invalid or missing token", Code = "401" });
                }

                // client se aaya hua path ignore/sanitize karke empId-scoped path banao
                string empRootRelative = "/" + empId;
                string clientSubPath = (path ?? "/").TrimStart('/');
                string effectivePath = string.IsNullOrEmpty(clientSubPath)
                    ? empRootRelative + "/"
                    : empRootRelative + "/" + clientSubPath;

                if (!effectivePath.EndsWith("/"))
                    effectivePath += "/";

                string empPhysicalRoot = Path.Combine(_root, empId);
                string empRootFullPath = Path.GetFullPath(empPhysicalRoot);

                if (!Directory.Exists(empPhysicalRoot))
                {
                    Directory.CreateDirectory(empPhysicalRoot);
                }

                path = effectivePath;

                // ---- Sub-folder creation (filename me agar "/" ho, jaise "folder/file.txt") ----
                foreach (var file in uploadFiles)
                {
                    var folders = file.FileName.Split('/');
                    if (folders.Length > 1)
                    {
                        for (var i = 0; i < folders.Length - 1; i++)
                        {
                            string newDirectoryPath = Path.Combine(_root + path, folders[i]);

                            string fullPath = Path.GetFullPath(newDirectoryPath);
                            if (!fullPath.StartsWith(empRootFullPath, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                            }

                            if (!Directory.Exists(newDirectoryPath))
                            {
                                _operation.Create(path, folders[i]);
                            }
                            path += folders[i] + "/";
                        }
                    }
                }

                // ---- Final target folder exist na kare to create kar do ----
                string finalTargetPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager", path.TrimStart('/'));
                string finalFullPath = Path.GetFullPath(finalTargetPath);

                if (!finalFullPath.StartsWith(empRootFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                }

                // ---- Action-wise branching ----
                switch (action?.ToLower())
                {
                    case "save":
                        {
                            if (!Directory.Exists(finalTargetPath))
                            {
                                Directory.CreateDirectory(finalTargetPath);
                            }

                            // Same naam ki file already ho to pehle usse remove karo (overwrite)
                            foreach (var file in uploadFiles)
                            {
                                var fileNameOnly = Path.GetFileName(file.FileName);
                                string existingFilePath = Path.Combine(finalTargetPath, fileNameOnly);

                                string existingFileFullPath = Path.GetFullPath(existingFilePath);
                                if (!existingFileFullPath.StartsWith(empRootFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                                }

                                if (System.IO.File.Exists(existingFilePath))
                                {
                                    System.IO.File.Delete(existingFilePath);
                                }
                            }

                            var uploadResponse = _operation.Upload(path, uploadFiles, action);
                            if (uploadResponse.Error != null)
                            {
                                return StatusCode(Convert.ToInt32(uploadResponse.Error.Code), uploadResponse);
                            }
                            return Ok(uploadResponse);
                        }

                    case "delete":
                        {
                            if (!Directory.Exists(finalTargetPath))
                            {
                                return Ok(new { message = "Path does not exist, nothing to delete." });
                            }

                            if (uploadFiles != null && uploadFiles.Count > 0)
                            {
                                // uploadFiles me diye gaye naam ke hisaab se specific files delete karo
                                foreach (var file in uploadFiles)
                                {
                                    var fileNameOnly = Path.GetFileName(file.FileName);
                                    string targetFilePath = Path.Combine(finalTargetPath, fileNameOnly);

                                    string targetFileFullPath = Path.GetFullPath(targetFilePath);
                                    if (!targetFileFullPath.StartsWith(empRootFullPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                                    }

                                    if (System.IO.File.Exists(targetFilePath))
                                    {
                                        System.IO.File.Delete(targetFilePath);
                                    }
                                }
                            }
                            else
                            {
                                var allFiles = Directory.GetFiles(finalTargetPath);
                                foreach (var f in allFiles)
                                {
                                    System.IO.File.Delete(f);
                                }
                            }

                            return Ok(new { message = "File(s) deleted successfully." });
                        }



                    case "View":
                        {
                            dynamic res;
                            Dictionary<int, string> data = new Dictionary<int, string>();
                            if (!Directory.Exists(finalTargetPath))
                            {
                                return Ok(new { message = "Path does not exist, nothing to delete." });
                            }

                            if (uploadFiles != null && uploadFiles.Count > 0)
                            {
                             
                            }
                            else
                            {
                                var allFiles = Directory.GetFiles(finalTargetPath);
                                int i = 1;
                                foreach (var f in allFiles)
                                {
                                    data.Add(i, f);
                                    i++;
                                }
                            }

                            res = new {
                                message = "File(s) deleted successfully.",
                                DataList = data
                            };

                            return Ok(res);
                        }
                    default:
                        return BadRequest(new ErrorDetails { Message = "Invalid action specified.", Code = "400" });
                }
            }
            catch (Exception e)
            {
                var errorResponse = new ErrorDetails
                {
                    Message = "Access denied for Directory-traversal",
                    Code = "417"
                };
                return StatusCode(Convert.ToInt32(errorResponse.Code), errorResponse);
            }
        }


        [Route("DownloadM")]
        [HttpPost]
        public IActionResult DownloadM([FromForm] string downloadInput)
        {
            dynamic res;
            var args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return res = new { state = true, message = _operation.Download(args.Path, args.Names, args.Data) };
        }

        [Route("GetImageM")]
        [HttpGet]
        public IActionResult GetImageM([FromQuery] string path, [FromQuery] string id)
        {
            dynamic res;
            try {
                var args = new FileManagerDirectoryContent
                {
                    Path = path,
                    Id = id
                };
                res = new
                {
                    state = true,
                    message = _operation.GetImage(args.Path, args.Id, false, null, null)
                };
            }
            catch (Exception ex) {
                res = new
                {
                    state = false,
                    Message = ex.Message
                };
            }
            return Ok(res);
        }
        private string GetEmpId()
        {
            var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            var handler = new JwtSecurityTokenHandler();
            var TokenDecription = handler.ReadJwtToken(getToken);
            string Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            return Empid;
        }
    
    }
}
