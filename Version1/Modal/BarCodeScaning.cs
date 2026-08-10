using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace SQCScanner.Modal
{
    public class BarCodeScaning
    {
        public static string ReadBarcode(Image<Rgba32> bubble)
        {
            // Step-1: ImageSharp → BMP MemoryStream
            using var ms = new MemoryStream();
            bubble.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var bitmap = new System.Drawing.Bitmap(ms);

            // Step-2: Convert Bitmap → Mat
            using var mat = BitmapConverter.ToMat(bitmap);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

            // Step-3: Preprocessing (Adaptive threshold)
            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray, thresh,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.Binary,
                21, 5);

            // Step-4: Remove noise
            using var denoise = new Mat();
            Cv2.MedianBlur(thresh, denoise, 3);

            // Step-5: Save debugging (optional)
            //Cv2.ImWrite($"wwwroot/alignedImages/barcode_debug_{Guid.NewGuid()}.png", denoise);

            // Step-6: ZXing reader options
            var options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new[]
                {
                    BarcodeFormat.ITF,         
                    BarcodeFormat.CODE_128,
                    BarcodeFormat.CODE_39,
                    BarcodeFormat.CODE_93,
                    BarcodeFormat.CODABAR,
                    BarcodeFormat.EAN_13,
                    BarcodeFormat.EAN_8,
                    BarcodeFormat.UPC_A,
                    BarcodeFormat.UPC_E,
                    BarcodeFormat.QR_CODE
                }
            };

            var reader = new BarcodeReader
            {
                AutoRotate = true,
                TryInverted = true,
                Options = options
            };

            // Step-7: Try decode on original
            var result = reader.Decode(bitmap);
            if (result != null)
                return result.Text;

            // Step-8: Try decode on processed OpenCV image
            using var finalBmp = denoise.ToBitmap();
            result = reader.Decode(finalBmp);

            return result?.Text ?? "❌ Barcode not detected.";
        }
    }
}
