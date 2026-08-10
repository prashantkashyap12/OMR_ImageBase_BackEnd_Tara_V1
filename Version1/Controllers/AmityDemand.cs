using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;
using SQCScanner.Services;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;

namespace SQCScanner.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AmityDemand : ControllerBase
    {
        private readonly MargeGenSerivce _margeGenSerivce;
        private readonly string _connStr;
        private readonly IConfiguration _configration;

        public AmityDemand(MargeGenSerivce margeGenSer, IConfiguration configration)
        {
            _margeGenSerivce = margeGenSer;
            _connStr = configration.GetConnectionString("dbc");

        }

        [HttpPost("MargeCSV")]
        public async Task<IActionResult> MargeExcel([FromForm] margeCSV model)
        {
            if (model.CSV1 == null || model.CSV2 == null)
            {
                return BadRequest("Both CSV files required");
            }
            var mergedData = _margeGenSerivce.MergeCsvFiles(model.CSV1, model.CSV2, model.CommonKey, model.ignoreColumns, model.Filer_key);
            return Ok(mergedData);
        }


        [HttpPost("GenerateResultExcel")]
        public async Task<IActionResult> GenerateResultExcel([FromForm] ResultRequest request)
        {
            if (request.AnswerKey == null || request.BubbleScan == null)
                return BadRequest("Files missing");

            // 👉 Extra Columns
            var extraCols = string.IsNullOrEmpty(request.selectedHeaders)
                ? new List<string>()
                : request.selectedHeaders.Split(',').Select(x => x.Trim()).ToList();

            // 👉 Subject-wise question mapping
            var subjectWiseFields = new Dictionary<string, List<string>>();

            var questionList = string.IsNullOrEmpty(request.questions)
                ? new List<QuestionRange>()
                : JsonConvert.DeserializeObject<List<QuestionRange>>(request.questions);

            if (questionList != null && questionList.Any())
            {
                foreach (var q in questionList)
                {
                    int start = int.Parse(q.startQ.Replace("Q", ""));
                    int end = int.Parse(q.endQ.Replace("Q", ""));

                    var list = new List<string>();

                    for (int i = start; i <= end; i++)
                    {
                        list.Add("Q" + i);
                    }

                    subjectWiseFields[q.subject] = list;
                }
            }

            // 👉 Read AnswerKey (CSV + Excel)
            Dictionary<string, string> answerKey = new Dictionary<string, string>();

            if (IsExcel(request.AnswerKey))
            {
                using (var stream = new MemoryStream())
                {
                    await request.AnswerKey.CopyToAsync(stream);
                    using (var wb = new XLWorkbook(stream))
                    {
                        var ws = wb.Worksheet(1);
                        var header = ws.Row(1);
                        var values = ws.Row(2);

                        for (int i = 1; i <= header.CellCount(); i++)
                        {
                            string key = header.Cell(i).GetString().Trim();
                            answerKey[key] = values.Cell(i).GetString();
                        }
                    }
                }
            }
            else
            {
                var csvData = ReadCSV(request.AnswerKey);
                if (csvData.Any())
                    answerKey = csvData.First();
            }

            // 👉 Read BubbleScan (CSV + Excel)
            List<Dictionary<string, string>> rowsData;

            if (IsExcel(request.BubbleScan))
            {
                rowsData = new List<Dictionary<string, string>>();

                using (var stream = new MemoryStream())
                {
                    await request.BubbleScan.CopyToAsync(stream);
                    using (var wb = new XLWorkbook(stream))
                    {
                        var ws = wb.Worksheet(1);
                        var headers = ws.Row(1).Cells().Select(x => x.GetString().Trim()).ToList();

                        foreach (var row in ws.RowsUsed().Skip(1))
                        {
                            var dict = new Dictionary<string, string>();

                            for (int i = 0; i < headers.Count; i++)
                            {
                                dict[headers[i]] = row.Cell(i + 1).GetString();
                            }

                            rowsData.Add(dict);
                        }
                    }
                }
            }
            else
            {
                rowsData = ReadCSV(request.BubbleScan);
            }

            var resultData = new List<Dictionary<string, object>>();

            foreach (var row in rowsData)
            {
                var obj = new Dictionary<string, object>();

                if (!row.ContainsKey(request.selectedKey))
                    continue;

                string rollValue = row[request.selectedKey];
                obj[request.selectedKey] = rollValue;

                // 👉 Extra Columns
                foreach (var ex in extraCols)
                {
                    obj[ex] = row.ContainsKey(ex) ? row[ex] : "";
                }

                int totalCorrect = 0;
                int totalWrong = 0;
                double totalMarks = 0;

                foreach (var subject in subjectWiseFields)
                {
                    int correct = 0;
                    int wrong = 0;
                    double marks = 0;

                    foreach (var field in subject.Value)
                    {
                        string studentAns = row.ContainsKey(field) ? row[field] : "";

                        if (answerKey.ContainsKey(field))
                        {
                            if (answerKey[field] == studentAns)
                            {
                                correct++;
                                marks += request.positive;
                            }
                            else if (!string.IsNullOrEmpty(studentAns))
                            {
                                wrong++;
                                marks -= request.negative;
                            }
                        }
                    }

                    obj[$"{subject.Key}_Correct"] = correct;
                    obj[$"{subject.Key}_Wrong"] = wrong;
                    obj[$"{subject.Key}_Marks"] = marks;

                    totalCorrect += correct;
                    totalWrong += wrong;
                    totalMarks += marks;
                }

                obj["TotalCorrect"] = totalCorrect;
                obj["TotalWrong"] = totalWrong;
                obj["TotalMarks"] = totalMarks;

                double percent = 0;

                if (request.percentage)
                {
                    int totalQ = subjectWiseFields.Sum(x => x.Value.Count);
                    percent = totalQ > 0 ? (double)totalCorrect / totalQ * 100 : 0;
                    obj["Percentage"] = percent;
                }

                if (request.grade)
                {
                    string gradeVal = percent >= 90 ? "A+" :
                                      percent >= 75 ? "A" :
                                      percent >= 60 ? "B" :
                                      percent >= 40 ? "C" : "F";

                    obj["Grade"] = gradeVal;
                }

                resultData.Add(obj);
            }

            // 👉 Generate CSV Output
            var sb = new StringBuilder();

            if (resultData.Any())
            {
                var headers = resultData.First().Keys.ToList();
                sb.AppendLine(string.Join(",", headers));

                foreach (var item in resultData)
                {
                    var row = headers.Select(h => Escape(item.ContainsKey(h) ? item[h]?.ToString() : ""));
                    sb.AppendLine(string.Join(",", row));
                }
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());

            return File(csvBytes, "text/csv", "Result.csv");
        }

        // ✅ CSV Reader
        private List<Dictionary<string, string>> ReadCSV(IFormFile file)
        {
            var data = new List<Dictionary<string, string>>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var headerLine = reader.ReadLine();
                var headers = headerLine.Split(',');

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    var dict = new Dictionary<string, string>();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        dict[headers[i].Trim()] = i < values.Length ? values[i].Trim() : "";
                    }

                    data.Add(dict);
                }
            }

            return data;
        }

        // ✅ Excel Check
        private bool IsExcel(IFormFile file)
        {
            return Path.GetExtension(file.FileName).ToLower() == ".xlsx";
        }

        // ✅ CSV Escape (comma safe)
        private string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }

        [HttpGet("export-db")]
        public IActionResult ExportDatabase()
        {
            string backupPath = Path.Combine(Directory.GetCurrentDirectory(), "backup.bak");

            string query = $@"
            BACKUP DATABASE [VersionSin321]
            TO DISK = '{backupPath}'
            WITH FORMAT, INIT";
            
            string connStr = "Data Source=DESKTOP-SOFT16;Initial Catalog=VersionSin321;Integrated Security=True;TrustServerCertificate=True;";
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.ExecuteNonQuery();
            }

            var bytes = System.IO.File.ReadAllBytes(backupPath);
            return File(bytes, "application/octet-stream", "backup.bak");
        }

        [HttpPost("import-db")]
        public async Task<IActionResult> ImportDatabase(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File not selected");
            }

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Backup");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string filePath = Path.Combine(uploadsFolder, file.FileName);

            // Save uploaded file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string masterConn = _connStr.Replace("Initial Catalog=VersionSin321", "Initial Catalog=master");

            using (SqlConnection conn = new SqlConnection(masterConn))
            {
                conn.Open();
                string query = $@"
                ALTER DATABASE [VersionSin321] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [VersionSin321]
                FROM DISK = '{filePath}'
                WITH REPLACE;
                ALTER DATABASE [VersionSin321] SET MULTI_USER;
                ";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 0; // long restore ke liye
                cmd.ExecuteNonQuery();
            }
            return Ok("Database Restored Successfully");
        }
    }
}

public class CsvRow
{
    public Dictionary<string, string> Data { get; set; } = new();
}
public class QuestionRange
{
    public string subject { get; set; }
    public string startQ { get; set; }
    public string endQ { get; set; }
}
public class ResultRequest
{
    public IFormFile AnswerKey { get; set; }
    public IFormFile BubbleScan { get; set; }
    public int positive { get; set; }
    public double negative { get; set; }
    public string selectedKey { get; set; }
    public string selectedHeaders { get; set; }
    public bool percentage { get; set; }
    public bool grade { get; set; }
    public string questions { get; set; }
}
public class margeCSV
{
    public IFormFile CSV1 { get; set; }
    public IFormFile CSV2 { get; set; }
    public string CommonKey { get; set; }
    public string ignoreColumns { get; set; } = "";
    public string Filer_key { get; set; } = "";

}
