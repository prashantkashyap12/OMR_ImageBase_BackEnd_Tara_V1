using OpenCvSharp;

namespace SQCScanner.Services
{
    public class SizedMatchedClass
    {
        public static string ResizeImageToTemplateSize(
            string imagePath,
            string templateImagePath)
        {
            // -----------------------------------------
            // 1. Template image se Width / Height lo
            // -----------------------------------------

            using var templateImage = Cv2.ImRead(
                templateImagePath,
                ImreadModes.Color
            );

            if (templateImage.Empty())
            {
                throw new Exception(
                    $"Template image not found or invalid: {templateImagePath}"
                );
            }

            int targetWidth = templateImage.Width;
            int targetHeight = templateImage.Height;


            // -----------------------------------------
            // 2. Original image read karo
            // -----------------------------------------

            using var sourceImage = Cv2.ImRead(
                imagePath,
                ImreadModes.Color
            );

            if (sourceImage.Empty())
            {
                throw new Exception(
                    $"Source image not found or invalid: {imagePath}"
                );
            }


            // -----------------------------------------
            // 3. Agar already same size hai
            // -----------------------------------------

            if (sourceImage.Width == targetWidth &&
                sourceImage.Height == targetHeight)
            {
                return imagePath;
            }


            // -----------------------------------------
            // 4. Resize image
            // -----------------------------------------

            using var resizedImage = new Mat();

            Cv2.Resize(
                sourceImage,
                resizedImage,
                new OpenCvSharp.Size(
                    targetWidth,
                    targetHeight
                ),
                0,
                0,
                InterpolationFlags.Lanczos4
            );


            // -----------------------------------------
            // 5. Same path + same filename + same extension
            // temporary file
            // -----------------------------------------

            string directory = Path.GetDirectoryName(imagePath)!;
            string fileName = Path.GetFileName(imagePath);
            string extension = Path.GetExtension(imagePath);

            string tempPath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(fileName)
                + "Temp"
                + extension
            );


            // -----------------------------------------
            // 6. Same extension ke saath save karo
            // -----------------------------------------

            bool saved = Cv2.ImWrite(
                tempPath,
                resizedImage
            );

            if (!saved)
            {
                throw new Exception(
                    $"Failed to save resized image: {tempPath}"
                );
            }


            // -----------------------------------------
            // 7. Original file replace karo
            // -----------------------------------------

            try
            {
                File.Delete(imagePath);

                File.Move(
                    tempPath,
                    imagePath
                );
            }
            catch
            {
                // Agar replace fail hua to temp file
                // clean kar do
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }


            // -----------------------------------------
            // 8. SAME imagePath return karo
            // -----------------------------------------

            return imagePath;
        }
    }
}
