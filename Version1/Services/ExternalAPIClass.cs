
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SQCScanner.Services
{
    public class ExternalAPIClass
    {


        private const string OcrApiUrl = "http://3.142.234.83:8085/api/textract";

        private static readonly string UnlabeledFolder = @"D:\Prashant_Devloper\ImageBaseOMR\FrontEnd\OMR_ImageBase_BackEnd_V1\Version1\wFileManager\bulk_scan\Text ERROR\Unlabeled";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };


        public static string ReadNumber(Image<Rgba32> charBox)
        {
            try
            {
                // Convert image to base64 string
                using var ms = new MemoryStream();
                charBox.Save(ms, new PngEncoder());
                string base64 = Convert.ToBase64String(ms.ToArray());

                var payload = new
                {
                    base64 = base64,
                    featureTypes = new[] { "TABLES" }
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = Http.PostAsync(OcrApiUrl, content).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"DigitTemplateReader: API HTTP error {(int)response.StatusCode}: {responseBody}");
                    SaveForReview(charBox, "api_error");
                    return string.Empty;
                }

                string rawText = ExtractMainLineText(responseBody);
                string digitsOnly = Regex.Replace(rawText ?? string.Empty, @"[^0-9]", "");

                if (string.IsNullOrEmpty(digitsOnly))
                    SaveForReview(charBox, "no_digits");

                return digitsOnly;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DigitTemplateReader Error: {ex.Message}");
                SaveForReview(charBox, "exception");
                return string.Empty;
            }
        }

        private static string? ExtractMainLineText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("Blocks", out var blocks) ||
                    blocks.ValueKind != JsonValueKind.Array)
                    return null;

                string? bestText = null;
                double bestArea = -1;

                foreach (var block in blocks.EnumerateArray())
                {
                    if (!block.TryGetProperty("BlockType", out var blockType) ||
                        blockType.GetString() != "LINE")
                        continue;

                    if (!block.TryGetProperty("Text", out var textEl) ||
                        textEl.ValueKind != JsonValueKind.String)
                        continue;

                    double width = 0, height = 0;
                    if (block.TryGetProperty("Geometry", out var geo) &&
                        geo.TryGetProperty("BoundingBox", out var box))
                    {
                        if (box.TryGetProperty("Width", out var w)) width = w.GetDouble();
                        if (box.TryGetProperty("Height", out var h)) height = h.GetDouble();
                    }

                    double area = width * height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestText = textEl.GetString();
                    }
                }

                return bestText;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"DigitTemplateReader: failed to parse API response: {ex.Message}");
                return null;
            }
        }

        private static void SaveForReview(Image<Rgba32> charBox, string tag)
        {
            try
            {
                Directory.CreateDirectory(UnlabeledFolder);
                charBox.SaveAsPng(Path.Combine(UnlabeledFolder, $"{tag}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DigitTemplateReader: review save failed: {ex.Message}");
            }
        }






    }
}
