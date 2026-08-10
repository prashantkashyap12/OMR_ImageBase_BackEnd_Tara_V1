using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace SQCScanner.Services
{
    public class ImgSave 
    {
        private readonly IWebHostEnvironment _env;
        public ImgSave(IWebHostEnvironment env)
        {
            _env = env;
        }


        public async Task<IActionResult> ScanedSave(string root, string imgPath, int templateId, bool status)
        {
            dynamic res = "";
            try
            {

                var getFile = Path.GetFileName(imgPath);
                var folderPathMain = "";
                if (status)
                {
                   folderPathMain = Path.Combine(root, "ScannedImg/", $"Template_{templateId}");
                }
                else
                {
                    folderPathMain = Path.Combine(root, "RejectImg/", $"Template_{templateId}");
                }

                if (!Directory.Exists(folderPathMain))
                {
                    Directory.CreateDirectory(folderPathMain);
                }
                folderPathMain = Path.Combine(folderPathMain, getFile);
                
                // Check file Already Saved.
                if (!File.Exists(folderPathMain))
                {
                    // Save Img  
                    using (var ImgTemp = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
                    using (var TempSet = new FileStream(folderPathMain, FileMode.Create, FileAccess.Write))
                    {
                        await ImgTemp.CopyToAsync(TempSet);
                    }
                    
                    res = new
                    {
                        message = "File Save Into TemplateWise n FolderName",
                        state = true,
                    };
                 
                }
                else
                {
                    res = new
                    {
                        message = "Already File Save Into TemplateWise n FolderName.",
                        state = false,
                    };
                }
                if (File.Exists(imgPath))     // img will be delete as per user desier 
                {
                    File.Delete(imgPath);
                }

            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }

            Console.WriteLine(res);
            return new JsonResult(res);
        }

        public async Task<IActionResult> userprofile(string empId, IFormFile imagepath)
        {
            dynamic res;
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "ProfilePicture", empId);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                string[] files = Directory.GetFiles(folderPath);
                foreach(var data in files)
                {
                    System.IO.File.Delete(data);
                }
            }
            if (imagepath != null && imagepath.Length > 0)
            {
                string filePath = Path.Combine(folderPath, imagepath.FileName);
                using var image = await Image.LoadAsync(imagepath.OpenReadStream());
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(800, 800)
                }));
                await image.SaveAsJpegAsync(filePath, new JpegEncoder
                {
                    Quality = 60
                });
                res = new
                {
                    state = true,
                    message = imagepath
                };
                return new JsonResult(res);
            }
            else
            {
                res = new
                {
                    state = false,
                    message = "File Not Saved"
                };
                return new JsonResult(res);
            }
        }
    }
}
