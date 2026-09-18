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

        // Folder where every crop + its preprocessed variants are saved for debugging.
        private static readonly string ErrorImageFolder =
            @"D:\Prashant_Devloper\ImageBaseOMR\FrontEnd\OMR_ImageBase_BackEnd_V1\Version1\wFileManager\bulk_scan\Text ERROR";

        public string ReadChar(Image<Rgba32> charBox)
        {
            // Unique id so all debug images from THIS call group together and sort together.
            string debugId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}".Substring(0, 26);

            try
            {
                // Save the raw crop first — this is what came IN, before any processing.
                // If this already looks bad, the problem is upstream (cropping), not OCR.
                SaveDebugImage(charBox, $"{debugId}_0_original.png");

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
                string bestVariantLabel = string.Empty;

                bool needsUpscale = charBox.Height < 60 || charBox.Width < 60;
                const int scale = 4; // bigger upscale helps Tesseract a lot on small crops

                using var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                // Helps on small, isolated character boxes. Change to SingleLine if the box
                // actually contains multiple digits in a row.
                engine.SetVariable("tessedit_pageseg_mode", "10");

                // Try both normal and inverted (white-on-black) polarity, since scanned boxes
                // sometimes come out inverted depending on threshold/lighting.
                foreach (var invert in new[] { false, true })
                {
                    foreach (var angle in RotationAngles)
                    {
                        using var candidate = charBox.Clone(ctx =>
                        {
                            if (needsUpscale)
                            {
                                ctx.Resize(charBox.Width * scale, charBox.Height * scale, KnownResamplers.Lanczos3);
                            }

                            if (angle != 0f)
                            {
                                ctx.Rotate(angle);
                            }

                            // --- Enhancement pipeline ---
                            ctx.Grayscale();
                            ctx.GaussianBlur(0.6f);      // light denoise before thresholding
                            ctx.AutoOrient();
                            ctx.Contrast(1.4f);
                            ctx.Brightness(1.05f);
                            ctx.BinaryThreshold(0.55f);  // hard black/white — big OCR accuracy boost

                            if (invert)
                            {
                                ctx.Invert();
                            }
                        });

                        string variantLabel = $"rot{(int)angle}_{(invert ? "inv" : "norm")}";

                        using var stream = new MemoryStream();
                        candidate.SaveAsPng(stream);
                        stream.Position = 0;
                        var bytes = stream.ToArray();

                        // Save EVERY variant Tesseract actually sees — this is the key debug artifact.
                        SaveDebugBytes(bytes, $"{debugId}_1_{variantLabel}.png");

                        using var pix = Pix.LoadFromMemory(bytes);
                        using var page = engine.Process(pix, PageSegMode.SingleChar);

                        string rawText = page.GetText();
                        string digitsOnly = new string(rawText.Where(char.IsDigit).ToArray());
                        float confidence = page.GetMeanConfidence();

                        if (!string.IsNullOrEmpty(digitsOnly) && confidence > bestConfidence)
                        {
                            bestConfidence = confidence;
                            bestResult = digitsOnly;
                            bestVariantLabel = variantLabel;
                        }
                    }
                }

                Console.WriteLine(string.IsNullOrEmpty(bestResult)
                    ? $"OCR: no digit found for {debugId}. Check saved images in '{ErrorImageFolder}'."
                    : $"OCR: '{bestResult}' (confidence {bestConfidence:F1}, best variant: {bestVariantLabel}) for {debugId}.");

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
                {
                    Directory.CreateDirectory(ErrorImageFolder);
                }

                image.SaveAsPng(Path.Combine(ErrorImageFolder, fileName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: debug image save karte waqt error aaya: {ex.Message}");
            }
        }

        private static void SaveDebugBytes(byte[] pngBytes, string fileName)
        {
            try
            {
                if (!Directory.Exists(ErrorImageFolder))
                {
                    Directory.CreateDirectory(ErrorImageFolder);
                }

                File.WriteAllBytes(Path.Combine(ErrorImageFolder, fileName), pngBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: debug image save karte waqt error aaya: {ex.Message}");
            }
        }
    }
}