using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SQCScanner.Modal;
using Version1.Data;
using Version1.Modal;
using Microsoft.Extensions.Logging;

namespace SQCScanner.Services
{
    public class FindCordinationClass
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FindCordinationClass> _logger;

        public FindCordinationClass(ApplicationDbContext context, ILogger<FindCordinationClass> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Main method to process the image and JSON template
        public async Task<string> FindCordinationAsync(string imageUrl, string jsonUrl)
        {
            try
            {
                // Load the image into ImageSharp for initial handling
                using var demoImage = Image.Load<Rgba32>(imageUrl);
                using var ms2 = new MemoryStream();
                demoImage.SaveAsBmp(ms2);
                ms2.Seek(0, SeekOrigin.Begin);
                using var bmp2 = new System.Drawing.Bitmap(ms2);
                using var demoImg = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp2);

                // Read the JSON template asynchronously
                var templateJson = await File.ReadAllTextAsync(jsonUrl);
                var template = JObject.Parse(templateJson);

                // Process the template and bubbles
                string markedPath2 = "";
                foreach (var field in template["fields"])
                {
                    double bubbleIntensity = field["bubbleIntensity"]?.Value<double>() ?? 0.3;
                    string fieldType = field["fieldType"]!.ToString();

                    // Validate fieldType and fieldValue
                    if (string.IsNullOrWhiteSpace(fieldType))
                    {
                        _logger.LogError("Missing 'fieldType' in field: {FieldName}", field["fieldName"]);
                        throw new ArgumentException("Missing 'fieldType' in field: must be \"formfield\" or \"questionfield\"");
                    }
                    string fieldValue = field["fieldValue"]!.ToString();
                    if (string.IsNullOrWhiteSpace(fieldValue))
                    {
                        _logger.LogError("Missing 'fieldValue' in field: {FieldName}", field["fieldName"]);
                        throw new ArgumentException("Missing 'fieldValue' in field: must be \"Integer\", \"Alphabet\", or \"Custom\"");
                    }

                    string fieldname = field["fieldName"]!.ToString();
                    var bubblesArray = field["bubbles"]?.ToObject<List<BubbleInfo>>();
                    if (bubblesArray != null)
                    {
                        var bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();
                        foreach (var pt in bubbleRects)
                        {
                            int x = (int)pt.X;
                            int y = (int)pt.Y;
                            int width = pt.Width;
                            int height = pt.Height;
                            int diameter = Math.Min(pt.Width, pt.Height);
                            OpenCvSharp.Point topLeftBub = new OpenCvSharp.Point(x, y);
                            OpenCvSharp.Point bottomRightBub = new OpenCvSharp.Point(x + width, y + height);
                            Cv2.Rectangle(demoImg, topLeftBub, bottomRightBub, new Scalar(0, 0, 255), 1);
                        }
                    }
                }
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager/ShubhamTEST");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                markedPath2 = Path.Combine(directoryPath, $"marked_{Guid.NewGuid()}.png");
                demoImg.SaveImage(markedPath2);

                // Check if the image is still valid (not empty)
                if (demoImg.Empty())
                {
                    _logger.LogError("Failed to process the image, image is empty.");
                    throw new InvalidOperationException("Failed to process the image.");
                }

                return markedPath2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the image and template.");
                throw;  
            }
        }

   
    }
}
