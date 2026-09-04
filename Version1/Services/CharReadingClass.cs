using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace SQCScanner.Services
{
    public class CharReadingClass
    {
        // Rotation angles to try — box orientation can vary, so we don't assume a fixed angle.
        private static readonly float[] RotationAngles = { 0f, 90f, 180f, 270f };

        // Resolve tessdata path ONCE relative to the app's actual bin folder,
        // never relative to the process's current working directory.
        private static readonly string TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

        public static string ReadChar(Image<Rgba32> charBox)
        {
            try
            {
                charBox.SaveAsPng("wFileManager\\ScanResult\\TemplateImages\\check\\bulk_scan\\Crop");

                // Fail fast with a clear message instead of the opaque
                // "Failed to initialise tesseract engine" error.
                if (!Directory.Exists(TessDataPath))
                {
                    Console.WriteLine($"OCR Error: tessdata folder not found at '{TessDataPath}'. " +
                        "Make sure the 'tessdata' folder is set to Copy to Output Directory (Copy if newer) in the project, " +
                        "and that eng.traineddata exists inside it.");
                    return string.Empty;
                }

                string trainedDataFile = Path.Combine(TessDataPath, "eng.traineddata");
                if (!File.Exists(trainedDataFile))
                {
                    Console.WriteLine($"OCR Error: 'eng.traineddata' not found at '{trainedDataFile}'. " +
                        "Download it from https://github.com/tesseract-ocr/tessdata and place it in the tessdata folder.");
                    return string.Empty;
                }

                string bestResult = string.Empty;
                float bestConfidence = -1f;
                bool needsUpscale = charBox.Height < 40 || charBox.Width < 40;

                using var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "0123456789");

                foreach (var angle in RotationAngles)
                {
                    using var candidate = charBox.Clone(ctx =>
                    {
                        if (needsUpscale)
                        {
                            const int scale = 3;
                            ctx.Resize(charBox.Width * scale, charBox.Height * scale, KnownResamplers.Lanczos3);
                        }

                        if (angle != 0f)
                        {
                            ctx.Rotate(angle);
                        }

                        ctx.Grayscale();
                        ctx.Contrast(1.2f);
                    });

                    using var stream = new MemoryStream();
                    candidate.SaveAsPng(stream);
                    stream.Position = 0;

                    using var pix = Pix.LoadFromMemory(stream.ToArray());
                    using var page = engine.Process(pix, PageSegMode.SingleLine);

                    string rawText = page.GetText();
                    string digitsOnly = new string(rawText.Where(char.IsDigit).ToArray());
                    float confidence = page.GetMeanConfidence();

                    if (!string.IsNullOrEmpty(digitsOnly) && confidence > bestConfidence)
                    {
                        bestConfidence = confidence;
                        bestResult = digitsOnly;
                    }
                }

                return bestResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}