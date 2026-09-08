using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace SQCScanner.Services
{
    public class CharReadingClass
    {
        private static readonly float[] RotationAngles = { 0f }; //, 90f, 180f, 270f
        private static readonly string TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        private static readonly string ErrorImageFolder = @"D:\Prashant_Devloper\ImageBaseOMR\FrontEnd\OMR_ImageBase_BackEnd_V1\Version1\wFileManager\bulk_scan\Text ERROR";

        public static string ReadChar(Image<Rgba32> charBox)
        {
            string debugId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}".Substring(0, 26);

            try
            {
                //SaveDebugImage(charBox, $"{debugId}_0_original.png");

                if (!Directory.Exists(TessDataPath))
                    return string.Empty;

                string bestResult = string.Empty;
                float bestConfidence = -1f;

                // 1. Line/Text sequence ke liye Engine Mode & PSM setup
                using var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "0123456789");

                // Single Line mode (PSM 7) multiple digits ke liye perfect hai
                engine.SetVariable("tessedit_pageseg_mode", "7");

                const int scale = 3;

                foreach (var angle in RotationAngles)
                {
                    // Variant 1: Adaptive Contrast Enhancement (Faded digits ke liye)
                    // Variant 2: Light Adaptive Thresholding
                    //for (int variant = 1; variant <= 2; variant++)
                    //{
                        using var candidate = charBox.Clone(ctx =>
                        {
                            // Resize (Bicubic scaling keeps thin strokes intact)
                            ctx.Resize(charBox.Width * scale, charBox.Height * scale, KnownResamplers.Bicubic);
                            //if (angle != 0f)
                            //    ctx.Rotate(angle);
                            ctx.Grayscale();
                            ctx.HistogramEqualization();
                            ctx.Contrast(1.5f);
                            
                        });

                        using var stream = new MemoryStream();
                        candidate.SaveAsPng(stream);
                        var bytes = stream.ToArray();

                        SaveDebugBytes(bytes, $"{debugId}_v_rot{(int)angle}.png");

                        using var pix = Pix.LoadFromMemory(bytes);

                        // PageSegMode.SingleLine enforce karein multiple digits read karne ke liye
                        using var page = engine.Process(pix, PageSegMode.SingleLine);

                        string rawText = page.GetText();
                        string digitsOnly = new string(rawText.Where(char.IsDigit).ToArray());
                        float confidence = page.GetMeanConfidence();

                        if (!string.IsNullOrEmpty(digitsOnly) && confidence > bestConfidence)
                        {
                            bestConfidence = confidence;
                            bestResult = digitsOnly;
                        }
                    //}
                }

                return bestResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: {ex.Message}");
                return string.Empty;
            }
        }

        private static void SaveDebugImage(Image<Rgba32> image, string fileName)
        {
            try
            {
                if (!Directory.Exists(ErrorImageFolder))
                    Directory.CreateDirectory(ErrorImageFolder);

                image.SaveAsPng(Path.Combine(ErrorImageFolder, fileName));
            }
            catch { }
        }

        private static void SaveDebugBytes(byte[] pngBytes, string fileName)
        {
            try
            {
                if (!Directory.Exists(ErrorImageFolder))
                    Directory.CreateDirectory(ErrorImageFolder);

                File.WriteAllBytes(Path.Combine(ErrorImageFolder, fileName), pngBytes);
            }
            catch { }
        }
    }
}