using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Version1.Data;
using Version1.Modal;
using TesseractOCR;
using Tesseract;
using System;
using OpenCvSharp.Extensions;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json.Linq;
using System.Drawing.Design;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using TesseractOCR.Renderers;
using SQCScanner.Modal;
using System.Text.RegularExpressions;
using System.Drawing.Text;
using Microsoft.AspNetCore.Http;
using Syncfusion.EJ2.Navigations;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SixLabors.ImageSharp.Formats.Png;
using Syncfusion.EJ2.Spreadsheet;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using OpenCvSharp.Extensions;
using Microsoft.AspNetCore.Routing.Template;
using Syncfusion.EJ2.Inputs;
using Swashbuckle.AspNetCore.SwaggerGen;
using ZXing.PDF417.Internal;
using ZXing;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OpenCvSharp.Aruco;
using CvSize = OpenCvSharp.Size;

namespace Version1.Services
{
    public class OmrProcessingService
    {
        private readonly ApplicationDbContext _context;

        public OmrProcessingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Point2f> demoImg = new List<Point2f>();
        public List<Point2f> ScanningImg = new List<Point2f>();

        public async Task<OmrResult> ProcessOmrSheet(string imagePath, string templatePath, string imageUrl, int ser, string userName)
        {



            string debugging = Path.GetFileName(Path.GetDirectoryName(imagePath));

            string alignedImages = $"wFileManager/ScanResult/TemplateImages/{userName}/{debugging}";
            Directory.CreateDirectory(alignedImages);
            var result = new OmrResult                         // Make model get Img name and make Dictionary <key, value>
            {
                FileName = Path.GetFileName(imagePath),
                FieldResults = new Dictionary<string, string>()
            };

            result.FieldResults["Sr"] = $"{ser}";
            var templateJson = File.ReadAllText(templatePath);  // Json k har Stringify karta hai.
            var template = JObject.Parse(templateJson);         // String ko Parse karta object banata hai.
            using var demoImage = Image.Load<Rgba32>(imageUrl);
            using var originalImage = Image.Load<Rgba32>(imagePath);
            // 1. Rotated Detacions -- OPEN
            var exif = originalImage.Metadata.ExifProfile;
            if (exif != null)
            {
                // Orientation tag nikalne ki koshish
                var orientation = exif.GetValue(ExifTag.Orientation);
                if (orientation != null)
                {
                    int orientationValue = (int)orientation.Value;
                    int rotationDegrees = orientationValue switch
                    {
                        1 => 0,
                        3 => 180,
                        6 => 90,
                        8 => 270,
                        _ => 0
                    };
                    result.FieldResults["Rotation"] = $"-{rotationDegrees}";
                }
                else
                {
                    Console.WriteLine("Orientation tag not found in EXIF metadata.");
                }
            }
            else
            {
                Console.WriteLine("No EXIF metadata found.");
            }

            // Demo 
            using var ms2 = new MemoryStream();                          // RAM ke andar ek virtual file
            demoImage.SaveAsBmp(ms2);                                       // Convert into Bit Image Sharp and Open Cv k liye 
            ms2.Seek(0, SeekOrigin.Begin);
            using var bmp2 = new System.Drawing.Bitmap(ms2);
            using var demoImg = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp2);   // convert into matrix

            // Scaning
            using var ms = new MemoryStream();                              // RAM ke andar ek virtual file
            originalImage.SaveAsBmp(ms);                                    // Convert into Bit Image Sharp and Open Cv k liye 
            ms.Seek(0, SeekOrigin.Begin);                       
            using var bmp = new System.Drawing.Bitmap(ms);  
            using var matInput = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp);   // convert into matrix
            string fileName2 = $"aligned_{Guid.NewGuid()}.png";
            string outputPath2 = Path.Combine(alignedImages, fileName2);
            using var mat = Cv2.ImRead(imagePath);                             //  Read with OpenCV
            //Cv2.ImWrite(outputPath2, mat);                                  //  Save processed image
            //originalImage.Save(outputPath2);


            var template2 = JObject.Parse(templateJson);
            
            // Testing Auto Rotate Methord     -- REWORK on Auto Rotation
            //var FindRotated = await RotateAndCheckMarkers(matInput, template2);
            Mat RotatedFinal = new Mat();
            RotatedFinal = matInput;         //-- Jab bhi kabhi hum work karnege wo usko "RotatedFinal"
            //if (result.Success)
            //{
            //    RotatedFinal = FindRotated.FinalImage;
            //    string debug2Path = Path.Combine(alignedImages, $"FinalRotate_{Guid.NewGuid()}.png");
            //    //RotatedFinal.SaveImage(debug2Path);
            //}
            //else
            //{
            //    result.Success = false;
            //    result.FieldResults["Report"] = "Image Is not Rotated ";
            //    return result;
            //}

            // Demo Image cordications
            var MatImgOut = await AlignWithTemplate(demoImg, template2, "demo");

            // Scanning Image cordications
            var MatImgOut2 = await AlignWithTemplate(RotatedFinal, template2, "scan");         

            if ((MatImgOut == null && MatImgOut2 == null))
            {
                result.Success = false;
                result.FieldResults["Report"] = "Skew markers are not filled or missing. Cannot proceed with OMR processing.";

                string outputPath6 = Path.Combine($"{alignedImages}/ERROR", result.FileName);
                Cv2.ImWrite(outputPath6, mat);
                return result;
            }

            Point2f[] demoDetaction = MatImgOut
                .Select(p => new Point2f(p.X, p.Y))
                .ToArray();

            //Point2f[] scaningDetaction = MatImgOut2.Select(p => new Point2f(p.X, p.Y)).ToArray();


            Point2f[] scaningDetaction = null;

            if (MatImgOut2 != null)
            {
                scaningDetaction = MatImgOut2
                    .Select(p => new Point2f(p.X, p.Y))
                    .ToArray();
            }

            // Detected corners mark (Green) and SAVE
            foreach (var pt in demoDetaction)
            {
                Cv2.Circle(demoImg, (int)pt.X, (int)pt.Y, 1, new Scalar(0, 255, 0), -1);
            }
            string markedPath = Path.Combine(alignedImages, $"marked_{Guid.NewGuid()}.png");
            //demoImg.SaveImage(markedPath);

            // Detact corners mark scaning and SAVE
            foreach (var pt in scaningDetaction)
            { 
                Cv2.Circle(RotatedFinal, (int)pt.X, (int)pt.Y, 1, new Scalar(0, 0, 255), -1);
            }
            string markedPath2 = Path.Combine(alignedImages, $"marked_{Guid.NewGuid()}.png");
            //matInput.SaveImage(markedPath2);

            // Convert into Wrap image <old image cordination > New Image Cordination) and SAVE
            Mat aligned = new Mat();
            if(demoDetaction.Length == 4 || scaningDetaction.Length == 4)
            {
                Mat homography = Cv2.GetPerspectiveTransform(scaningDetaction, demoDetaction);
                Cv2.WarpPerspective(RotatedFinal, aligned, homography, demoImg.Size());
            }
            else if (demoDetaction.Length == 3 && scaningDetaction.Length == 3)
            {
                Mat H = Cv2.GetAffineTransform(scaningDetaction, demoDetaction);
                Cv2.WarpAffine(RotatedFinal, aligned, H, demoImg.Size());
                markedPath2 = Path.Combine(alignedImages, $"wraped_{Guid.NewGuid()}.png");
                //aligned.SaveImage(markedPath2);
            }
            else if (demoDetaction.Length == 2 && scaningDetaction.Length == 2)
            {
                //aligned = FixWithTwoPoints(RotatedFinal, scaningDetaction, demoDetaction);
                aligned = FixWithTwoPointsAdvanced(
                       RotatedFinal,
                       scaningDetaction,
                       demoDetaction,
                       new CvSize(demoImg.Width, demoImg.Height)
                );
                markedPath2 = Path.Combine(alignedImages, $"marked_{Guid.NewGuid()}.png");
            }
            else
            {
                throw new Exception("Not enough alignment points for warp.");
            }

            string debugPath = Path.Combine(alignedImages, $"wrapPrespativ_{Guid.NewGuid()}.png");
            //aligned.SaveImage(debugPath);

            // Convert Mat to byte[] (e.g., PNG in memory)
            byte[] imageBytes = aligned.ToBytes(".png");
            Image<Rgba32> scanningImage = Image.Load<Rgba32>(imageBytes);
            // Bitmap bitmap = BitmapConverter.ToBitmap(aligned);

            // Continue All Process and SAVE (before bubble Scanning)
            var image = scanningImage.Clone();  // Move forword for scanning
            //string fileName11 = $"aligned_{Guid.NewGuid()}.png";
            //string outputPath11 = Path.Combine(alignedImages, fileName11);
            //image.Save(outputPath11);

            var debugImage = image.Clone();
            // Make clone init.

            // 3. Reffrence points check -- DONE   
            if (false)     
            {
               var referenceFields = template["referncefield"]?.ToArray();          // Reffrecne points ko array me return karta hai
                if (referenceFields != null && referenceFields.Length > 0)          // agr mila to check image par apply hai ya ni hai bubble detaction.
                {
                    //demoDetaction.Length
                    bool allFilled = AreReferenceMarkersFilled(image, template, scaningDetaction);    // Any One False to IMAGE Returning ERROR MSG
                    if (!allFilled)
                    {
                        result.Success = false;
                        result.FieldResults["Report"] = "Skew markers are not filled or missing. Cannot proceed with OMR processing.";
                        if (!Directory.Exists($"{alignedImages}/ReffrenceError/"))
                        {
                            Directory.CreateDirectory($"{alignedImages}/ReffrenceError/");

                        }
                        // Move ahead with error folder
                        string outputPath6 = Path.Combine($"{alignedImages}/ReffrenceError/", result.FileName);
                        image.Save(outputPath6);



                        // Move ahead with error folder

                        return result;
                    }
                }
            }

            // 4. Add Error DPI Range
            if (!IsDpiValid(image, out string dpiError))
            {
                return new OmrResult
                {
                    FileName = Path.GetFileName(imagePath),
                    Success = false,
                    FieldResults = new Dictionary<string, string>
                    {
                        { "Report", dpiError}
                    }
                };
            }

            // 5. Detact Angle of demo image BUT should be new Image.  <Soniya use less Code>
            double angle = CalculateSkewAngleFromMarkers(template);
            using var angledFix = DeskewImage(scanningImage, -angle);

            // ** Add fields scaning filed **
            var imgServ = Path.GetFileName(imagePath);           // Image File Name
            result.FieldResults["FileName"] = imgServ;           // Add New FileName into Dictronary

            // Bubble Detaction from fields
            foreach (var field  in template["fields"])
            {
                double bubbleIntensity = field["bubbleIntensity"]?.Value<double>() ?? 0.3;
                
                string fieldType = field["fieldType"]!.ToString();    // convert into string if blank return error
                if (string.IsNullOrWhiteSpace(fieldType))
                {
                    throw new ArgumentException($"Missing 'fieldType' in field: must be \"formfield\" or \"questionfield\"");
                }

                string fieldValue ="";
                if (fieldType != "barcode")
                {
                    fieldValue = field["fieldValue"]!.ToString();  // convert into string if blank return error
                    if (string.IsNullOrWhiteSpace(fieldValue))
                    {
                        throw new ArgumentException($"Missing 'fieldValue' in field: must be \"Integer\", \"Alphabet\", or \"Custom\"");
                    }
                }

                string fieldname = field["fieldName"]!.ToString();                              // extract value
                var bubblesArray = field["bubbles"]?.ToObject<List<BubbleInfo>>();              // extract value
                bool allowMultiple = field["allowMultiple"]?.Value<bool>() ?? true;             // extract value
                string blankOuputSymbol = field["blankOuputSymbol"]?.ToString() ?? "#";         // extract value
                string multipleBubbleOutput = field["multipleBubbleOutput"]?.ToString() ?? "*"; // extract value
                
                if (bubblesArray != null)
                {
                    List<Rectangle> bubbleRects;
                    if (fieldType != "barcode")
                    {
                        bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();
                    }
                    else
                    {
                        bubbleRects = new List<Rectangle>
                        {
                            new Rectangle(
                                (int)field["x"],
                                (int)field["y"],
                                (int)field["width"],
                                (int)field["height"]
                            )
                        };
                    }
                    //var bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();
                    foreach (var pt in bubbleRects)
                    {
                        int x = (int)pt.X; 
                        int y = (int)pt.Y; 
                        int width = pt.Width; 
                        int height = pt.Height; 
                        int diameter = Math.Min(pt.Width, pt.Height); 
                        
                        // Take the smaller of width and height
                        OpenCvSharp.Point topLeftBub = new OpenCvSharp.Point(x, y);
                        OpenCvSharp.Point bottomRightBub = new OpenCvSharp.Point(x + width, y + height);    
                        Cv2.Rectangle(aligned, topLeftBub, bottomRightBub, new Scalar(0, 0, 255), 1);

                        //Crop the region of interest(ROI) inside the bounding box
                        Mat roi = new Mat(aligned, new OpenCvSharp.Rect(x, y, width, height));

                        // ROI ko gray to binary
                        Mat grayCont = new Mat();
                        Cv2.CvtColor(roi, grayCont, ColorConversionCodes.BGR2GRAY);

                        // ROI ko Threshold image me convert karna
                        Mat binaryConvty = new Mat();
                        Cv2.Threshold(grayCont, binaryConvty, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                        // Blank Value
                        OpenCvSharp.Point[][] contoursValConvty;
                        HierarchyIndex[] _;
                        Cv2.FindContours(binaryConvty, out contoursValConvty, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                        Cv2.DrawContours(aligned, contoursValConvty, -1, new Scalar(225, 255, 0), 1);
                    }
                    Mat OMRSheet = new Mat();
                    
                    //  CLOSE -- here we will add drawing to design bubble cordination on image and share to isBubble fill methord
                    var options = field["Custom"]?.ToObject<List<string>>()?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                    if (field["Custom0"] != null && (options == null || options.Count == 0))
                    {
                        throw new ArgumentException($"The field '{fieldname}' includes an 'Custom' array but it is empty or invalid. Please provide valid Custom.");
                    }
                    if (options == null || options.Count == 0)
                    {
                        options = GenerateOptionsFromFieldType(fieldValue, bubblesArray);
                    }

                    string readdirection = field["ReadingDirection"]!.ToString();
                    bool Bestbubble = !field["best_bubble"]?.Value<bool>() ?? true;
                    var LithoCode = new List<string>();
                    if (fieldType == "formfield")    
                    {

                        // Filter Data Results 
                        bool isMerge = field["isMerged"]?.Value<bool>() ?? false;
                        if (isMerge)
                        {
                            List<string> responseList = new List<string>();
                            string fieldmerged = field["mergedInto"]!.ToString() ?? null;
                            var dobFields = template["fields"].Where(f => f["mergedInto"] != null && f["mergedInto"].ToString() == fieldmerged).ToList();
                            foreach (var fieldDupli in dobFields)
                            {
                                var bubblesArrayMarge = fieldDupli["bubbles"]?.ToObject<List<BubbleInfo>>();
                                var optionsMarge = fieldDupli["Custom"]?.ToObject<List<string>>()?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                                var bubbleRectsMarge = bubblesArrayMarge.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();
                                foreach (var pt in bubbleRectsMarge)
                                {
                                    int x = (int)pt.X;
                                    int y = (int)pt.Y;
                                    int width = pt.Width;
                                    int height = pt.Height;
                                    int diameter = Math.Min(pt.Width, pt.Height);

                                    // Take the smaller of width and height
                                    OpenCvSharp.Point topLeftBub = new OpenCvSharp.Point(x, y);
                                    OpenCvSharp.Point bottomRightBub = new OpenCvSharp.Point(x + width, y + height);
                                    Cv2.Rectangle(aligned, topLeftBub, bottomRightBub, new Scalar(0, 0, 255), 1);

                                    //Crop the region of interest(ROI) inside the bounding box
                                    Mat roi = new Mat(aligned, new OpenCvSharp.Rect(x, y, width, height));

                                    // ROI ko gray to binary
                                    Mat grayCont = new Mat();
                                    Cv2.CvtColor(roi, grayCont, ColorConversionCodes.BGR2GRAY);

                                    // ROI ko Threshold image me convert karna
                                    Mat binaryConvty = new Mat();
                                    Cv2.Threshold(grayCont, binaryConvty, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                                    // Blank Value
                                    OpenCvSharp.Point[][] contoursValConvty;
                                    HierarchyIndex[] _;
                                    Cv2.FindContours(binaryConvty, out contoursValConvty, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                                    Cv2.DrawContours(aligned, contoursValConvty, -1, new Scalar(225, 255, 0), 1);
                                }
                                var answers = ExtractAnswersFromBubbles(image, bubbleRectsMarge, bubblesArrayMarge, optionsMarge, readdirection, bubbleIntensity, allowMultiple, blankOuputSymbol, multipleBubbleOutput, Bestbubble);
                                var combined = string.Join("", answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", ""))).Select(kv => kv.Value).Where(val => val != "No bubble Mark" && val != "InvalidOption"));
                                Console.WriteLine(combined);
                                responseList.Add(combined);
                            }
                            string finalResponse = string.Join("", responseList);
                            result.FieldResults[fieldname] = finalResponse;
                        }
                        else
                        {
                            var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple, blankOuputSymbol, multipleBubbleOutput, Bestbubble);
                            var combined = string.Join("", answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", "")))
                            .Select(kv => kv.Value).Where(val => val != "No bubble Mark" && val != "InvalidOption"));
                            Console.WriteLine(combined);
                            result.FieldResults[fieldname] = combined; //combined.TrimEnd('*');
                        }
                    }
                    else if (fieldType == "questionfield")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple, blankOuputSymbol, multipleBubbleOutput, Bestbubble);
                        if (Regex.IsMatch(fieldname, @"q\d+-q\d+", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(fieldname, @"q(\d+)-q(\d+)", RegexOptions.IgnoreCase);
                            int start = int.Parse(match.Groups[1].Value);
                            int end = int.Parse(match.Groups[2].Value);

                            int index = 0;
                            for (int q = start; q <= end; q++)
                            {
                                string questionKey = $"Q{q}";
                                if (answers.TryGetValue($"Q{index + 1}", out string? val))
                                {
                                    result.FieldResults[questionKey] = val;
                                }
                                index++;
                            }
                        }
                        else
                        {
                            // Default behavior
                            foreach (var kv in answers)
                            {
                                if (!string.IsNullOrWhiteSpace(kv.Value) && kv.Value != "No bubble Mark" && kv.Value != "InvalidOption")
                                {
                                    result.FieldResults[kv.Key] = kv.Value;
                                }
                            }
                        }
                    }
                    else if (fieldType == "barcode")
                    {
                        var barX = field["x"].ToObject<int>();
                        var barY = field["y"].ToObject<int>();
                        var barW = field["width"].ToObject<int>();
                        var barH = field["height"].ToObject<int>();
                        var rect = new SixLabors.ImageSharp.Rectangle(barX, barY, barW, barH);
                        var region = image.Clone(ctx => ctx.Crop(rect));
                        dynamic imageval = image;
                        string barcodeValue = BarCodeScaning.ReadBarcode(region);

                        // move bar code error files
                        if (barcodeValue == "❌ Barcode not detected.")
                        {
                            if (!Directory.Exists($"{alignedImages}/BarCodeError/")){
                                Directory.CreateDirectory($"{alignedImages}/Error/");
                            }
                            string BarErrorName = $"{alignedImages}/Error/{result.FileName}";
                            image.Save(BarErrorName);

                            result.Success = false;
                            result.FieldResults["Report"] = "Barcode not Found";
                        }

                        // move bar code error files
                        result.FieldResults["BarCode"] = barcodeValue;
                    }
                    else if(fieldType == "lithocode")
                    {
                        var Lithodcode="";
                        foreach(var pt in bubblesArray){
                            int x = (int)pt.X;
                            int y = (int)pt.Y;
                            int width = pt.Width;
                            int height = pt.Height;
                            var rect = new SixLabors.ImageSharp.Rectangle(x, y, width, height);
                            var region = image.Clone(ctx => ctx.Crop(rect));
                            LithoCode.Add(IsBubbleFilled(region, bubbleIntensity) ? "1" : "0");
                        }
                        string concatenatedBinary = string.Join("", LithoCode);
                        string reversedBinary = new string(concatenatedBinary.Reverse().ToArray());
                        int decimalValue = Convert.ToInt32(reversedBinary, 2);
                        Console.WriteLine("Facing Error - " +decimalValue);
                        result.FieldResults["Lithocode"] = decimalValue.ToString();
                    }
                }
            }
            string markedPat2 = Path.Combine(alignedImages,result.FileName);
            aligned.SaveImage(markedPat2);
            result.ProcessedAt = DateTime.UtcNow;
            return result;
        }


        //0.Image should be auto rotated  -- NOT USING 
        public async Task<(Mat FinalImage, bool Success, string Message)> RotateAndCheckMarkers(
        Mat matImg, JObject template, double bubbleIntensity = 0.3)
        {
            var positions = new[] { "topLeft", "topRight", "bottomLeft", "bottomRight" };
            var reference = template["referncefield"]?[0];
            if (reference == null)
                return (matImg, false, "Reference field missing.");

            for (int rotation = 0; rotation < 4; rotation++)
            {
                byte[] data = matImg.ImEncode(".png");
                Image<Rgba32> image = Image.Load<Rgba32>(data);
                bool found = false;
                foreach (var pos in positions)
                {
                    var marker = reference[pos]?.ToObject<BubbleInfo>();
                    if (marker == null)
                        continue;

                    var rect = new SixLabors.ImageSharp.Rectangle(marker.X, marker.Y, marker.Width, marker.Height);
                    var region = image.Clone(ctx => ctx.Crop(rect));

                    if (IsBubbleFilled(region, bubbleIntensity))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    return (matImg, true, "Markers detected. Image correctly oriented.");
                }

                // Rotate if not found
                Mat rotated = new Mat();
                Cv2.Rotate(matImg, rotated, RotateFlags.Rotate90Clockwise);
                matImg = rotated;
            }
            Mat blank = new Mat(480, 640, MatType.CV_8UC3, new Scalar(0, 0, 0));
            return (blank, false, "Skew markers are not filled or missing. Cannot proceed with OMR processing.");
        }


        // 1. Image should be auto rotated.  
        //public async Task<Mat> rotateImge(Mat matImg, JObject template2)
        //{
        //    var referenceFields = template2["referncefield"]?.ToArray();
        //    var refField = template2["referncefield"]?.FirstOrDefault();
        //    var topLeft = refField["topLeft"]?.ToObject<BubbleInfo2Class>();
        //    int height = matImg.Rows;
        //    int distanceFromTop = topLeft.Y;
        //    OpenCvSharp.Point bubbleCenter = new OpenCvSharp.Point(topLeft.X + topLeft.Width / 2, topLeft.Y + topLeft.Height / 2);
        //    var rect = new OpenCvSharp.Rect(topLeft.X, topLeft.Y, topLeft.Width, topLeft.Height);
        //    for (int attempt = 0; attempt <= 3; attempt++)
        //    {
        //        Mat region1 = new Mat(matImg, rect);
        //        byte[] data = region1.ImEncode(".png");
        //        Image<Rgba32> bubbleImage = Image.Load<Rgba32>(data);
        //        if (IsBubbleFilled(bubbleImage, 1.6))
        //        {
        //            return matImg;
        //        }
        //        else
        //        {
        //            Mat rotated = new Mat();
        //            Cv2.Rotate(matImg, rotated, RotateFlags.Rotate90Clockwise);
        //            matImg = rotated;
        //            string alignedImages = "wwwroot/alignedImages/";
        //            string fileName2 = $"aligned_{Guid.NewGuid()}.png";
        //            string outputPath2 = Path.Combine(alignedImages, fileName2);
        //        }
        //    }
        //    var blankImg = new Mat(480, 640, MatType.CV_8UC3, new Scalar(0, 0, 0));
        //    return blankImg;
        //}

        // 2. Image Prespactive view _ from demo image refrence points.

        private List<Point2f> pointsList = new List<Point2f>();
        private List<Point2f> pointsList2 = new List<Point2f>();
        public async Task<List<Point2f>> AlignWithTemplate(Mat inputImage, JObject template, string SelctImg)
        {

            //List<Point2f> value = new List(Point2f);
            if (SelctImg == "demo")
                pointsList.Clear();
            else if (SelctImg == "scan") 
                pointsList2.Clear();

            return await Task.Run(() =>
            {
                // 1. Template ke reference points JSON se lo
                var refField = template["referncefield"]?.FirstOrDefault();
                if (refField == null)
                {
                    throw new Exception("Template JSON me 'referncefield' missing hai!");
                }

                // Allowed fixed key order
                string[] markerOrder = { "topLeft", "topRight", "bottomLeft", "bottomRight" };

                List<Point2f> templateCorners = new();
                foreach (var key in markerOrder)
                {
                    var obj = refField[key]?.ToObject<BubbleInfo2Class>();
                    if (obj != null)   
                    {
                        templateCorners.Add(new Point2f(obj.X, obj.Y));
                    }
                }

                // Agar kam se kam 1 marker mila hai to uske width/height
                int width = 0;
                int height = 0;
                if (templateCorners.Count > 0)
                {
                    var firstMarker = refField[markerOrder[0]]?.ToObject<BubbleInfo2Class>();
                    if (firstMarker != null)
                    {
                        width = firstMarker.Width;
                        height = firstMarker.Height;
                    }
                }

                // Template corners mark (Red)
                float cxGlobal = -1;
                float cyGlobal = -1;
                //var topExtra = 30;     // 

                var rotate = 0;
                int extra = 8;
                foreach (var pt in templateCorners)
                {
                    rotate = rotate + 1;
                    int y;
                    int x;
                    //if (rotate == 2)
                    //{
                    //    x = (int)pt.X-10;   
                    //    y = (int)pt.Y-35;
                    //    extra = 15;
                    //}
                    //if(rotate == 4)
                    //{
                    //    x = (int)pt.X - 10;
                    //    y = (int)pt.Y - 30;
                    //}
                    //else
                    //{
                    //    x = (int)pt.X - 10;
                    //    y = (int)pt.Y - 30;
                    //}

                    //x = (int)pt.X;
                    //y = (int)pt.Y;
                    //extra = 15;

                    // PART AMITY 4 - SYCONIC SCANNER -- 100% ACC
                    //if (rotate == 1)
                    //{
                    //    x = (int)pt.X;
                    //    y = (int)pt.Y - 10;
                    //    extra = 15;
                    //}
                    //else
                    //{
                    //    x = (int)pt.X;
                    //    y = (int)pt.Y;
                    //    extra = 15;
                    //}

                    // PART AMITY 3 - SYCONIC SCANNER  -- 100% ACC
                    if (rotate == 1)
                    {
                        x = (int)pt.X - 10;
                        y = (int)pt.Y - 20;
                        extra = 7;
                    }
                    else
                    {
                        x = (int)pt.X;
                        y = (int)pt.Y - 5;
                        extra = 10;
                    }


                    // make cordination variable
                    OpenCvSharp.Point topLeft1 = new OpenCvSharp.Point(x, y);
                    // Increase width and height into cordination
                    OpenCvSharp.Point bottomRight1 = new OpenCvSharp.Point(x + width, y + height);
                    // ROI rectangle -- 
                    int newX = Math.Max(0, x - extra);
                    int newY = Math.Max(0, y - extra);
                    int newWidth = Math.Min(inputImage.Width - newX, width + extra * 4);
                    int newHeight = Math.Min(inputImage.Height - newY, height +extra * 4);
                    OpenCvSharp.Rect Rect2 = new OpenCvSharp.Rect(newX, newY, newWidth, newHeight);
                    using var roiImage = new Mat(inputImage, Rect2);
                    // ROI ko gray to binary
                    Mat gray = new Mat();
                    Cv2.CvtColor(roiImage, gray, ColorConversionCodes.BGR2GRAY);
                    // ROI ko Threshold image me convert karna
                    Mat binary = new Mat();
                    Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                    // Blank Value
                    OpenCvSharp.Point[][] contoursVal;
                    HierarchyIndex[] _;
                    Cv2.FindContours(binary, out contoursVal, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                    Cv2.DrawContours(roiImage, contoursVal, -1, new Scalar(225, 255, 0), 1);
                    // Old - OPEN
                    int maxIndexFound = -1;
                    var widthArea = width;
                    var heightArea = height;
                    if (contoursVal.Length > 0)
                    {
                        // Big Bubble 
                        maxIndexFound = contoursVal
                            .Select((c, i) => new { Area = Cv2.ContourArea(c), Index = i })
                            .OrderByDescending(c => c.Area)
                            .First().Index;


                            
                        // Circle contour Points Range wise -- OPEN ||  contoursVal array se max points X,Y index range 15 to 40 se match karna. 
                        //int validMin = 15;
                        //int validMax = 60;
                        //var validContours =
                        //    contoursVal
                        //    .Select((c, i) => new { Points = c.Length, Index = i })
                        //    .Where(x => x.Points >= validMin && x.Points <= validMax)
                        //    .ToList();
                        //Console.WriteLine(validContours[0].Index);
                        //maxIndexFound = validContours[0].Index;
                        //int finalIndex = 0;
                        //if (validContours.Count > 0)
                        //{
                        //    finalIndex = validContours
                        //        .Select(v => new
                        //        {
                        //            Area = Cv2.ContourArea(contoursVal[v.Index]),
                        //            Index = v.Index
                        //        })
                        //        .OrderByDescending(x => x.Area)
                        //        .First()
                        //        .Index;
                        //}else
                        //{
                        //    cxGlobal = 0;
                        //    cyGlobal = 0;
                        //    if (SelctImg == "demo")
                        //        pointsList.Add(new Point2f(0, 0));
                        //    else if (SelctImg == "scan")
                        //        pointsList2.Add(new Point2f(0, 0));
                        //}
                        // Circle Range wise -- CLOSE ||


                        // Working AREA START
                        //double tolerancePercent = 25;  // 25% tolerance
                        //bool bubbleFound = false;
                        //double minW = widthArea - (widthArea * tolerancePercent / 100.0);
                        //double maxW = widthArea + (widthArea * tolerancePercent / 100.0);
                        //double minH = heightArea - (heightArea * tolerancePercent / 100.0);
                        //double maxH = heightArea + (heightArea * tolerancePercent / 100.0);

                        //for (int i = 0; i < contoursVal.Length; i++)
                        //{
                        //    var rect = Cv2.BoundingRect(contoursVal[i]);

                        //    // Height/Width tolerance check
                        //    if (rect.Width >= minW && rect.Width <= maxW &&
                        //        rect.Height >= minH && rect.Height <= maxH)
                        //    {
                        //        maxIndexFound = i;   // Yeh hi sahi bubble hai
                        //        bubbleFound = true;
                        //        break;
                        //    }
                        //}

                        //if (!bubbleFound)
                        //{
                        //    throw new Exception($"Bubble not found within tolerance range! (Width:{widthArea}, Height:{heightArea})");
                        //    break;
                        //}
                        // Working AREA CLOSE



                        var M = Cv2.Moments(contoursVal[maxIndexFound]);
                        if (M.M00 != 0)
                        {
                            float cx = (float)(M.M10 / M.M00);
                            float cy = (float)(M.M01 / M.M00);

                            OpenCvSharp.Point localCenter = new OpenCvSharp.Point((int)cx, (int)cy);
                            OpenCvSharp.Point globalCenter = new OpenCvSharp.Point(
                                (int)(cx + Rect2.X),
                                (int)(cy + Rect2.Y)
                            );
                            cxGlobal = cx + Rect2.X;
                            cyGlobal = cy + Rect2.Y;

                            if (SelctImg == "demo")
                            {
                                pointsList.Add(new Point2f(cxGlobal, cyGlobal));
                            }
                            else if (SelctImg == "scan")
                            {
                                pointsList2.Add(new Point2f(cxGlobal, cyGlobal));
                            }
                        }
                    }
                }
                return (SelctImg == "demo") ? pointsList : pointsList2;
            });
        }

        //string outputFolder2 = "wwwroot/alignedImages/";
        //Directory.CreateDirectory(outputFolder2);
        //string fileName2 = $"aligned_{Guid.NewGuid()}.png";
        //string outputPath2 = Path.Combine(outputFolder2, fileName2);
        //inputImage.SaveImage(outputPath2);
        //Mat aligned = new Mat();
        // Add in array list of cordinations.
  

        public void AddPoint(float x, float y)
        {
            // Create a new Point2f object and add it to the list
            Point2f newPoint = new Point2f(x, y);
            pointsList.Add(newPoint);
        }

        // 3. Image k Refrence field TEST   --  
        private bool AreReferenceMarkersFilled(Image<Rgba32> image, JObject template, Point2f[] scaningDetaction, double bubbleIntensity = 0.1)
        {
            // Find Refrence mark. form array
            var referenceFields = template["referncefield"]?.ToArray();


            // agr Reffrence mark nahi diye from JSON to True otherwise NOTHING
            if (referenceFields == null || referenceFields.Length == 0)
                return true;

            // Pahli Refrence field li jati hai.   agr 2/3 rahi to error ayegi abhi to 4 hai    
            var refMarker = referenceFields[0];
            var positions = new[] { "topLeft", "topRight", "bottomLeft", "bottomRight" };
            var points = new List<PointF>();
            var points2 = new List<PointF>();

            // For courner k liye ek loop 
            foreach (var refrance in positions)
            {
                var obj = refMarker[refrance]?.ToObject<BubbleInfo2Class>();
                if (obj != null)
                {
                    points2.Add(new PointF(obj.X, obj.Y));
                }
            }
            foreach (var pos in positions)
            {
                var marker = refMarker[pos]?.ToObject<BubbleInfo>();
                points.Add(new PointF(marker.X + marker.Width / 2f, marker.Y + marker.Height / 2f));
                if (marker == null) return false;
                var rect = new SixLabors.ImageSharp.Rectangle(marker.X, marker.Y, marker.Width, marker.Height);
                var region = image.Clone(ctx => ctx.Crop(rect));

                if (!IsBubbleFilled(region, bubbleIntensity))
                {
                    return false;
                }
            }
            return true;
        }

        // 4. Add Error DPI Range  _ Calling Funcation
        private bool IsDpiValid(Image<Rgba32> imagePath, out string errorMassage)  // << here we will use fresh scanning image 
        {
            using var ms = new MemoryStream();
            imagePath.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var bitmap = new System.Drawing.Bitmap(ms);
            float dpiX = bitmap.HorizontalResolution;
            float dpiY = bitmap.VerticalResolution;

            if (dpiX < 95 || dpiY < 95)
            {
                errorMassage = $"Image DPI is too law DPI shpuld be at least 100";
                return false;
            }
            errorMassage = string.Empty;
            return true;
        }

        // 5. Check Skew Angle Refrance Marks   -- Use less 
        private double CalculateSkewAngleFromMarkers(JObject template)
        {
            var refField = template["referncefield"]?.FirstOrDefault();
            if (refField == null) return 0;
            var topLeft = refField["topLeft"]?.ToObject<BubbleInfo>();
            var topRight = refField["topRight"]?.ToObject<BubbleInfo>();
            if (topLeft == null || topRight == null) return 0;
            double dx = topRight.X - topLeft.X;
            double dy = topRight.Y - topLeft.Y;
            double angleRadians = Math.Atan2(dy, dx);
            double angleDegrees = angleRadians * (180.0 / Math.PI);
            return angleDegrees;
        }
        private Image<Rgba32> DeskewImage(Image<Rgba32> image, double angle)
        {
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            using var bitmap = new System.Drawing.Bitmap(ms);
            using var mat = BitmapConverter.ToMat(bitmap);

            var center = new OpenCvSharp.Point2f(mat.Width / 2, mat.Height / 2);
            var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);    // Rotation matrix create karna.
            Cv2.WarpAffine(mat, mat, rotationMatrix, mat.Size());                // Rotate karna.
            using var rotatedBmp = BitmapConverter.ToBitmap(mat);
            using var mem = new MemoryStream();
            rotatedBmp.Save(mem, System.Drawing.Imaging.ImageFormat.Bmp);
            mem.Seek(0, SeekOrigin.Begin);
            return Image.Load<Rgba32>(mem.ToArray());
        }

        // Make grid setting as per Range.
        private List<string> GenerateOptionsFromFieldType(string fieldValue, List<BubbleInfo> bubbles)
        {
            if (fieldValue == "Integer")
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();
                return Enumerable.Range(0, RowCount).Select(i => i.ToString()).ToList();
            }

            if (fieldValue.Equals("Alphabet", StringComparison.OrdinalIgnoreCase))
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();
                int optStr;
                if (colCount > 1)
                {
                    optStr = colCount;
                }
                else if(RowCount > 1)
                {
                    optStr = RowCount;
                }
                else
                {
                    optStr = 1;
                }
                return Enumerable.Range(0, optStr)
                    .Select(i => ((char)('A' + i)).ToString())
                    .ToList(); ;
            }
            return new List<string>();
        }

        // Checking Horizontal Or Vertical direction me Bubble Check kar k result return karta hai.  // Direction ko validate karna 
        private Dictionary<string, string> ExtractAnswersFromBubbles(Image<Rgba32> image, List<Rectangle> bubbleRects, List<BubbleInfo> bubbleInfos, List<string> options,
         string? readdirection, double bubbleIntensity, bool allowMultiple, string blankOuputSymbol, string multipleBubbleOutput, bool Bestbubble)  
        {

            if (string.IsNullOrWhiteSpace(readdirection))
                throw new ArgumentException("You must provide 'ReadingDirection' in the template. Allowed values: 'Horizontal' or 'Vertical'.");

            readdirection = readdirection.Trim();

            if (readdirection != "Row" && readdirection != "Column")
                throw new ArgumentException($"Invalid 'ReadingDirection': '{readdirection}'. Allowed values: 'Horizontal' or 'Vertical'.");

            // Bubble Grouping 
            var result = new Dictionary<string, string>();
            var grouped = (readdirection == "Row")
                ? bubbleInfos.GroupBy(b => b.Row).OrderBy(g => g.Key)
                : bubbleInfos.GroupBy(b => b.Col).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                int questionIndex = group.Key;
                var filledOptions = new List<string>();
                var sortedGroup = (readdirection == "Row")
                    ? group.OrderBy(b => b.Col)
                    : group.OrderBy(b => b.Row);

                int bubbleInGroupIndex = 0;
                var densityList = new List<(double Density, string Option)>();
                foreach (var bubble in sortedGroup)
                { 
                    int index = bubbleInfos.IndexOf(bubble);
                    if (index < 0 || index >= bubbleRects.Count)
                        continue;

                    int padding = 0;
                    var originalRect = bubbleRects[index];
                    var paddedRect = new Rectangle(
                        Math.Max(0, originalRect.X - padding),
                        Math.Max(0, originalRect.Y - padding),
                        Math.Min(originalRect.Width + 2 * padding, image.Width - originalRect.X + padding),
                        Math.Min(originalRect.Height + 2 * padding, image.Height - originalRect.Y + padding)
                    );
                    var cell2 = image.Clone(ctx => ctx.Crop(paddedRect));

                    // Top Dancity of bubbble 
                    (bool isBubbleFilled, double densityPercent) = IsBubbleFilledPre(cell2, bubbleIntensity);   // D Strecturing
                    if (isBubbleFilled)
                    {
                        int optionIndex = bubbleInGroupIndex;
                        if (optionIndex >= 0 && optionIndex < options.Count)
                        {
                            if (Bestbubble)
                            {   // Best bubble 
                                filledOptions.Add(options[optionIndex]);
                            }   
                            else
                            {   // Without Best Bubble
                                densityList.Add((densityPercent, options[optionIndex]));
                            }
                        }
                    } bubbleInGroupIndex++;
                }


                string output = "";
                if (Bestbubble)   
                {
                    if (filledOptions.Count == 0)
                    {
                        output = blankOuputSymbol;
                    }
                    else if (filledOptions.Count == 1)
                    {
                        output = filledOptions[0];
                    }
                    else if (filledOptions.Count > 1)
                    {
                        output = allowMultiple ? string.Join("", filledOptions) : multipleBubbleOutput;
                    }
                    result[$"Q{questionIndex + 1}"] = output;
                }
                else
                {
                    if (densityList.Count == 0)
                    {
                        output = blankOuputSymbol;
                    }
                    else
                    {
                        var maxEntry = densityList.OrderByDescending(x => x.Density).First();
                        output = maxEntry.Option;
                    }
                    result[$"Q{questionIndex + 1}"] = output;
                }
            }
            return result;
        }

        private (bool, double) IsBubbleFilledPre(Image<Rgba32> bubble, double bubbleIntensity)
        {
            using var ms = new MemoryStream();
            bubble.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            using var bitmap = new System.Drawing.Bitmap(ms);
            using var mat = BitmapConverter.ToMat(bitmap);

            string alignedImages = "wwwroot/alignedImages/";
            string fileName11 = $"aligned_{Guid.NewGuid()}.png";
            string outputPath11 = Path.Combine(alignedImages, fileName11);

            // Convert into Gray Scale 
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

            //Cv2.ImWrite(outputPath11, gray);
            using var binary = new Mat();

            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(3, 3), 0);
            Cv2.Threshold(gray, binary, 80, 255, ThresholdTypes.Binary);        // Dynamic for add range for mark less then not count = Link Sensitivity   
            //Cv2.ImWrite(outputPath11, gray);

            // Noise Remove with OpenCV
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)));
             //Cv2.ImWrite(outputPath11, binary);

            // Convert into negative black 
            using var inverted = new Mat();
            Cv2.BitwiseNot(binary, inverted);
            int blackPixels = Cv2.CountNonZero(inverted);
            bool resBub2 = blackPixels > bubbleIntensity;
            var addStack = new Dictionary<bool, int>();   
            int totalPixels = inverted.Rows * inverted.Cols;
            double density = (double)blackPixels / totalPixels;            
            double getDencityPercent = density * 100;
            return (resBub2, getDencityPercent);
        }

        private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity, bool mostFilled = false)
        {
            using var ms = new MemoryStream();
            bubble.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);

            //Save image into BMP
            using var bitmap = new System.Drawing.Bitmap(ms);

            // BitMap ko OpenCV matrix me convert
            using var mat = BitmapConverter.ToMat(bitmap);

            string alignedImages = "wwwroot/alignedImages/";
            string fileName11 = $"aligned_{Guid.NewGuid()}.png";
            string outputPath11 = Path.Combine(alignedImages, fileName11);

            //Convert into Gray Scale 
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            //Cv2.ImWrite(outputPath11, gray);
            // Throsholding (Gray into B/W img) 

            using var binary = new Mat();
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(3, 3), 0);
            //Cv2.ImWrite(outputPath11, gray);

            //using var adaptiveThresh = new Mat();
            //Cv2.Threshold(gray, binary, 50, 255, ThresholdTypes.Binary);                                                      // Already use
            Cv2.Threshold(gray, binary, 80, 255, ThresholdTypes.Binary);
            //Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);                                 // Test 1
            //Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);         // Test 2
            //Cv2.ImWrite(outputPath11, binary);

            // Noise Remove with OpenCV
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open,
            Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)));

            // Convert into negative black 
            using var inverted = new Mat();
            Cv2.BitwiseNot(binary, inverted);
            int blackPixels = Cv2.CountNonZero(inverted);

            bool resBub2 = blackPixels > bubbleIntensity;
            var addStack = new Dictionary<bool, int>();

            if (!mostFilled)
            {
                int totalPixels = inverted.Rows * inverted.Cols;
                double density = (double)blackPixels / totalPixels;
                int getPercent = (int)(density * 100);
                addStack.Add(resBub2, getPercent);
            }
            else
            {
                addStack.Add(resBub2, 0);
            }
            bool resBub = blackPixels > bubbleIntensity;
            return resBub;
        }

        //private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity)
        //{
        //    using var ms = new MemoryStream();
        //    bubble.SaveAsBmp(ms);
        //    ms.Seek(0, SeekOrigin.Begin);

        //    using var bitmap = new System.Drawing.Bitmap(ms);
        //    using var mat = BitmapConverter.ToMat(bitmap);

        //    string alignedImages = "wwwroot/alignedImages/";
        //    string fileName = $"aligned_{Guid.NewGuid()}.png";
        //    string outputPath = Path.Combine(alignedImages, fileName);

        //    using var gray = new Mat();
        //    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        //    Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(3, 3), 0);

        //    using var binary = new Mat();
        //    double[] thresholds = { 80, 60, 50 };

        //    foreach (var thresh in thresholds)
        //    {
        //        Cv2.Threshold(gray, binary, thresh, 255, ThresholdTypes.Binary);

        //        Cv2.MorphologyEx(binary, binary, MorphTypes.Open,
        //            Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)));

        //        using var inverted = new Mat();
        //        Cv2.BitwiseNot(binary, inverted);

        //        int filledPixels = Cv2.CountNonZero(inverted);
        //        double fillRatio = (double)filledPixels / (inverted.Rows * inverted.Cols);

        //        if (fillRatio > bubbleIntensity)
        //        {
        //            Cv2.ImWrite(outputPath, inverted);
        //            return true;
        //        }
        //    }

        //    // Optional: Save last attempt (lowest threshold) for debugging
        //    Cv2.ImWrite(outputPath, binary);
        //    return false;
        //}

        public Mat FixWithTwoPoints(Mat input, Point2f[] scanPts, Point2f[] demoPts)
        {
            // 1. Points
            Point2f s1 = scanPts[0];
            Point2f s2 = scanPts[1];
            Point2f d1 = demoPts[0];
            Point2f d2 = demoPts[1];

            // 2. Angle calculate
            double angle1 = Math.Atan2(s2.Y - s1.Y, s2.X - s1.X);
            double angle2 = Math.Atan2(d2.Y - d1.Y, d2.X - d1.X);
            double rotationAngle = (angle2 - angle1) * 180.0 / Math.PI;

            // 3. Rotate image around scan point 1
            Mat rotationMatrix = Cv2.GetRotationMatrix2D(s1, rotationAngle, 1.0);

            Mat rotated = new Mat();
            Cv2.WarpAffine(input, rotated, rotationMatrix, input.Size());

            // 4. Rotation ke baad scanPt1 ki new position
            double newX = rotationMatrix.At<double>(0, 0) * s1.X +
                          rotationMatrix.At<double>(0, 1) * s1.Y +
                          rotationMatrix.At<double>(0, 2);

            double newY = rotationMatrix.At<double>(1, 0) * s1.X +
                          rotationMatrix.At<double>(1, 1) * s1.Y +
                          rotationMatrix.At<double>(1, 2);

            // 5. Translation vector
            double shiftX = d1.X - newX;
            double shiftY = d1.Y - newY;

            // 6. Final affine transform matrix (rotation + translation)
            Mat finalMat = rotationMatrix.Clone();
            finalMat.Set(0, 2, finalMat.At<double>(0, 2) + shiftX);
            finalMat.Set(1, 2, finalMat.At<double>(1, 2) + shiftY);

            Mat output = new Mat();
            Cv2.WarpAffine(rotated, output, finalMat, input.Size());

            return output;
        }

        public Mat FixWithTwoPointsAdvanced(Mat src, Point2f[] scanPts, Point2f[] demoPts, CvSize outputSize)
        {
            if (scanPts.Length != 2 || demoPts.Length != 2)
                throw new Exception("Two points required");

            // Distance between scan points
            double scanDist = Math.Sqrt(
                Math.Pow(scanPts[1].X - scanPts[0].X, 2) +
                Math.Pow(scanPts[1].Y - scanPts[0].Y, 2)
            );

            // Distance between demo points
            double demoDist = Math.Sqrt(
                Math.Pow(demoPts[1].X - demoPts[0].X, 2) +
                Math.Pow(demoPts[1].Y - demoPts[0].Y, 2)
            );

            // Scale ratio
            double scale = demoDist / scanDist;

            // Angle of scan line
            double scanAngle = Math.Atan2(
                scanPts[1].Y - scanPts[0].Y,
                scanPts[1].X - scanPts[0].X
            );

            // Angle of demo line
            double demoAngle = Math.Atan2(
                demoPts[1].Y - demoPts[0].Y,
                demoPts[1].X - demoPts[0].X
            );

            // Rotation angle difference
            double angle = (demoAngle - scanAngle) * 180 / Math.PI;

            // Rotation center
            Point2f center = scanPts[0];

            // Rotation + scale matrix
            Mat rotMat = Cv2.GetRotationMatrix2D(center, angle, scale);

            // Translation correction
            double tx = demoPts[0].X - scanPts[0].X;
            double ty = demoPts[0].Y - scanPts[0].Y;

            rotMat.Set<double>(0, 2, rotMat.Get<double>(0, 2) + tx);
            rotMat.Set<double>(1, 2, rotMat.Get<double>(1, 2) + ty);

            Mat aligned = new Mat();

            Cv2.WarpAffine(
                src,
                aligned,
                rotMat,
                outputSize,
                InterpolationFlags.Linear,
                BorderTypes.Constant,
                Scalar.White
            );

            return aligned;
        }

        private void SetProperty(OmrResult result, string propName, string value)
        {
            var prop = typeof(OmrResult).GetProperty(propName);
            if (prop != null && prop.CanWrite)
                prop.SetValue(result, value);
        }
    }
}