using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SQCScanner.Services;

namespace SQCScanner.Controllers
{
    [EnableCors("AllowAnyOrigin")]
    [Route("api/[controller]")]
    [ApiController]
    public class OCRController : ControllerBase
    {
        private readonly ILogger _logger;

        public OCRController(ILogger<OCRController> logger)
        {
            _logger = logger;
        }


        [HttpPost]
        [Route("ImageReading")]
        public async Task<IActionResult> Get(IFormFile AttchCrop)
        {
            if (AttchCrop == null || AttchCrop.Length == 0)
            {
                return BadRequest("Image file is required.");
            }
            try
            {
                Image<Rgba32> charBox = await ConvertToRgba32(AttchCrop);

                //var reader = new CharReading2Class();
                //string dataReading = reader?.ReadAnswerSheetNumber(charBox);
                //Console.WriteLine(dataReading);


                var reader1 = new DigitTemplateReader();
                string dataReading = reader1.ReadNumber(charBox);
                Console.WriteLine(dataReading);


                //var reader2 = new CharReadingClass();
                //string dataReading = reader2.ReadChar(charBox);
                //Console.WriteLine(dataReading);


                return Ok("output ="+dataReading);
            }
            catch (Exception ex) { 
         
                return BadRequest($"Error processing image: {ex.Message}");
            }
        }


        private async Task<Image<Rgba32>> ConvertToRgba32(IFormFile file)
        {
            await using var stream = file.OpenReadStream();

            Image<Rgba32> image = await Image.LoadAsync<Rgba32>(stream);

            return image;
        }


    }
}
