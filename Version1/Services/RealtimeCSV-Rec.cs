using Newtonsoft.Json.Linq;
using Version1.Data;
using Version1.Modal;
using System.Text;
using System;
using OpenCvSharp.Aruco;

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
            string dirPath,
            Dictionary<string, string> record)
        {
            try
            {
                Directory.CreateDirectory(dirPath);

                string filePath = Path.Combine(dirPath, "Record.csv");

                bool fileExists = File.Exists(filePath);

                List<string> headers = new();

                // Read existing header
                if (fileExists)
                {
                    using var readFs = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite);

                    using var reader = new StreamReader(readFs);

                    string? headerLine = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(headerLine))
                    {
                        headers = ParseCsvLine(headerLine);
                    }
                }

                // Add new columns
                foreach (var key in record.Keys)
                {
                    if (!headers.Contains(key))
                    {
                        headers.Add(key);
                    }
                }

                // If new headers were added to an existing file,
                // the existing CSV needs to be rebuilt.
                bool headersChanged = !fileExists ||
                                      !headers.SequenceEqual(
                                          fileExists
                                              ? await ReadHeader(filePath)
                                              : Enumerable.Empty<string>());

                if (headersChanged && fileExists)
                {
                    // For a production system, use a temporary file
                    // and replace the original atomically.
                    await RewriteCsvWithNewHeaders(filePath, headers, record);
                }
                else
                {
                    string row = string.Join(",",
                        headers.Select(h =>
                            EscapeCsv(record.TryGetValue(h, out var value)
                                ? value
                                : "")));

                    using var fs = new FileStream(
                        filePath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite);

                    using var writer = new StreamWriter(
                        fs,
                        new UTF8Encoding(false));

                    if (!fileExists)
                    {
                        await writer.WriteLineAsync(
                            string.Join(",", headers.Select(EscapeCsv)));
                    }

                    await writer.WriteLineAsync(row);
                    await writer.FlushAsync();
                }

                return "Dynamic CSV record saved (Live)";
            }
            catch (IOException ex) when (ex.Message.Contains(
                "not enough space",
                StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"CSV ERROR: Disk is full. Path: {dirPath}");

                return "CSV save error: Disk space is full";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                return "CSV save error";
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            value = value.Replace("\"", "\"\"");

            if (value.Contains(',') ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r'))
            {
                return $"\"{value}\"";
            }

            return value;
        }

        private static async Task<List<string>> ReadHeader(
            string filePath)
        {
            using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = new StreamReader(fs);

            string? header = await reader.ReadLineAsync();

            return string.IsNullOrWhiteSpace(header)
                ? new List<string>()
                : ParseCsvLine(header);
        }

        private static List<string> ParseCsvLine(string line)
        {
            // Basic parser for headers.
            // If headers themselves can contain quoted commas,
            // use a proper CSV library.
            return line.Split(',').ToList();
        }

        private static async Task RewriteCsvWithNewHeaders(
            string filePath,
            List<string> headers,
            Dictionary<string, string> newRecord)
        {
            string tempPath = filePath + ".tmp";

            var existingRows = new List<Dictionary<string, string>>();

            // Read existing data
            using (var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                string? oldHeaderLine = await reader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(oldHeaderLine))
                {
                    var oldHeaders = ParseCsvLine(oldHeaderLine);

                    while (true)
                    {
                        string? line = await reader.ReadLineAsync();

                        if (line == null)
                            break;

                        var values = ParseCsvLine(line);

                        var row = new Dictionary<string, string>();

                        for (int i = 0; i < oldHeaders.Count; i++)
                        {
                            row[oldHeaders[i]] =
                                i < values.Count ? values[i] : "";
                        }

                        existingRows.Add(row);
                    }
                }
            }

            // Write new file
            using (var fs = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new StreamWriter(
                fs,
                new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync(
                    string.Join(",", headers.Select(EscapeCsv)));

                foreach (var oldRow in existingRows)
                {
                    var row = headers.Select(h =>
                        EscapeCsv(
                            oldRow.TryGetValue(h, out var value)
                                ? value
                                : ""));

                    await writer.WriteLineAsync(
                        string.Join(",", row));
                }

                var newRow = headers.Select(h =>
                    EscapeCsv(
                        newRecord.TryGetValue(h, out var value)
                            ? value
                            : ""));

                await writer.WriteLineAsync(
                    string.Join(",", newRow));

                await writer.FlushAsync();
            }

            File.Delete(filePath);
            File.Move(tempPath, filePath);
        }
    }
}
