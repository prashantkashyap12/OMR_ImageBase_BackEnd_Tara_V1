using OpenCvSharp;
using Tesseract;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace SQCScanner.Services
{
    public class CharReading2Class
    {
        private readonly string _tessDataPath;

        public CharReading2Class()
        {
            _tessDataPath = Path.Combine(
                AppContext.BaseDirectory,
                "tessdata"
            );
        }

        public string ReadAnswerSheetNumber(Image<Rgba32> charBox)
        {
            try
            {
                // -----------------------------------------
                // 1. Image<Rgba32> -> PNG bytes -> OpenCV Mat
                //    (direct decode, no temp file / no base64-as-path)
                // -----------------------------------------

                using var ms = new MemoryStream();
                charBox.Save(ms, new PngEncoder());
                byte[] inputBytes = ms.ToArray();

                using var image = Cv2.ImDecode(inputBytes, ImreadModes.Color);

                if (image.Empty())
                    return string.Empty;

                // -----------------------------------------
                // 2. charBox already number crop hai,
                //    isliye extra ROI crop yahan nahi lagana.
                // -----------------------------------------

                // -----------------------------------------
                // 3. Grayscale
                // -----------------------------------------

                using var gray = new Mat();

                Cv2.CvtColor(
                    image,
                    gray,
                    ColorConversionCodes.BGR2GRAY
                );

                // -----------------------------------------
                // 4. Resize 3X
                // -----------------------------------------

                using var resized = new Mat();

                Cv2.Resize(
                    gray,
                    resized,
                    new OpenCvSharp.Size(),
                    3,
                    3,
                    InterpolationFlags.Cubic
                );

                // -----------------------------------------
                // 5. Threshold
                // -----------------------------------------

                using var binary = new Mat();

                Cv2.Threshold(
                    resized,
                    binary,
                    0,
                    255,
                    ThresholdTypes.Binary | ThresholdTypes.Otsu
                );

                // -----------------------------------------
                // 6. Convert OpenCV Mat -> PNG bytes
                // -----------------------------------------

                Cv2.ImEncode(
                    ".png",
                    binary,
                    out byte[] imageBytes
                );

                // -----------------------------------------
                // 7. Tesseract
                // -----------------------------------------

                using var engine = new TesseractEngine(
                    _tessDataPath,
                    "eng",
                    EngineMode.Default
                );

                // Sirf digits read karne hain
                engine.SetVariable(
                    "tessedit_char_whitelist",
                    "0123456789"
                );

                // PNG bytes -> Tesseract Pix
                using var pix = Pix.LoadFromMemory(imageBytes);

                // Single line OCR
                using var page = engine.Process(
                    pix,
                    PageSegMode.SingleLine
                );

                string text = page.GetText();

                // -----------------------------------------
                // 8. Only digits
                // -----------------------------------------

                string result = new string(
                    text.Where(char.IsDigit).ToArray()
                );

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Answer Sheet Number OCR Error: {ex.Message}"
                );

                return string.Empty;
            }
        }
    }
}