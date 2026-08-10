namespace SQCScanner.Services
{
    public class MargeGenSerivce
    {
        public List<Dictionary<string, string>> MergeCsvFiles(
            IFormFile csv1,
            IFormFile csv2,
            string key,
            string? ignoreColumns,
            string? Filer_key)
        {
            var data1 = ReadCsv(csv1);
            var data2 = ReadCsv(csv2);

            var ignoreList = string.IsNullOrEmpty(ignoreColumns)
                ? new List<string>()
                : ignoreColumns.Split(',')
                               .Select(x => x.Trim())
                               .ToList();

            var mergedData = MergeCsv(data1, data2, key, ignoreList);

            // ✅ Filter apply
            if (!string.IsNullOrEmpty(Filer_key))
            {
                var parts = Filer_key.Split(':');

                if (parts.Length == 2)
                {
                    string filterColumn = parts[0].Trim();
                    string filterValue = parts[1].Trim();

                    mergedData = mergedData
                        .Where(row =>
                            row.ContainsKey(filterColumn) &&
                            row[filterColumn]
                                .Equals(filterValue, StringComparison.OrdinalIgnoreCase)
                        )
                        .ToList();
                }
            }

            return mergedData;
        }

        // ✅ Merge CSV
        private List<Dictionary<string, string>> MergeCsv(
            List<Dictionary<string, string>> csv1,
            List<Dictionary<string, string>> csv2,
            string key,
            List<string> ignoreColumns)
        {
            var result = new List<Dictionary<string, string>>();

            var csv2Lookup = csv2
                .Where(x => x.ContainsKey(key))
                .ToDictionary(x => x[key], x => x);

            foreach (var row1 in csv1)
            {
                if (!row1.ContainsKey(key)) continue;

                var merged = new Dictionary<string, string>();

                foreach (var col in row1)
                {
                    if (!ignoreColumns.Contains(col.Key))
                    {
                        merged[col.Key] = col.Value;
                    }
                }

                if (csv2Lookup.TryGetValue(row1[key], out var row2))
                {
                    foreach (var col in row2)
                    {
                        if (!merged.ContainsKey(col.Key) && !ignoreColumns.Contains(col.Key))
                        {
                            merged[col.Key] = col.Value;
                        }
                    }
                }

                result.Add(merged);
            }

            return result;
        }

        // ✅ Read CSV
        private List<Dictionary<string, string>> ReadCsv(IFormFile file)
        {
            var result = new List<Dictionary<string, string>>();

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
                        dict[headers[i]] = i < values.Length ? values[i] : "";
                    }

                    result.Add(dict);
                }
            }

            return result;
        }
    }
}