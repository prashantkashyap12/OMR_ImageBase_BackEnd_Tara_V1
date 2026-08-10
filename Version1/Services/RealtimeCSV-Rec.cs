using Newtonsoft.Json.Linq;
using Version1.Data;
using Version1.Modal;
using System.Text;
using System;

namespace SQCScanner.Services
{
    public class RealtimeCSV_Rec
    {
        private readonly ApplicationDbContext _context;
        public RealtimeCSV_Rec(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> RealtimeCSV(
        string userId,
        int idTemp,
        Dictionary<string, string> record,
        string templateName,
        string folderPAth)
        {
            //wFileManager/ScanResult
            try
            {
                string dirPath = Path.Combine("wFileManager", "ScanResult", "CSV_Record", userId, templateName);
                Directory.CreateDirectory(dirPath);
                string safeFileName = folderPAth
                    .Replace("\\", "_")
                    .Replace("/", "_");

                string filePath = Path.Combine(dirPath, $"{safeFileName}_file.csv");
                bool fileExists = File.Exists(filePath);
                List<string> headers = new();
                if (fileExists)
                {
                    using var readFs = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite);

                    using var reader = new StreamReader(readFs);
                    string? headerLine = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(headerLine))
                    {
                        headers = headerLine.Split(',').ToList();
                    }
                }

                // 🔹 Add missing headers
                foreach (var key in record.Keys)
                {
                    if (!headers.Contains(key))
                        headers.Add(key);
                }

                // 🔹 Create row as per header order
                var row = headers
                    .Select(h => record.ContainsKey(h) ? record[h] : "")
                    .ToArray();

                // 🔹 Append mode (LIVE UPDATE SAFE)
                using var fs = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);

                using var writer = new StreamWriter(fs);

                // 🔹 Write header only once
                if (!fileExists)
                {
                    await writer.WriteLineAsync(string.Join(",", headers));
                }

                // 🔹 Write row
                await writer.WriteLineAsync(string.Join(",", row));
                await writer.FlushAsync(); // 🔥 very important
                return "Dynamic CSV record saved (Live)";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "CSV save error";
            }
        }
     }
}
