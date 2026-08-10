using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Version1.Modal;

namespace SQCScanner.Services
{
    public class RecordDBClass
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;


        public RecordDBClass(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
        }
        public async Task<List<string>> TableCreation(OmrResult respose, int templateId)
        {

            var fieldNames = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string tableName = $"Template_{templateId}";
                string checkTableSql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                var exists = await connection.QueryFirstOrDefaultAsync<int?>(checkTableSql, new { TableName = tableName });
                bool tableExists = exists.HasValue;
            
                var fields = respose.FieldResults.Select(fr => fr.Key).Distinct().ToList();
                fieldNames.AddRange(fields);
                if (!tableExists)
                {
                    var columnsSql = string.Join(", ", fields.Select(f => $"[{f}] NVARCHAR(MAX)"));
                    string createTableSql = $"CREATE TABLE [{tableName}] (Id INT IDENTITY(1,1) PRIMARY KEY, {"[LiveTime] NVARCHAR(MAX)"},{"[UserName] NVARCHAR(MAX)"}, {columnsSql})";
                    await connection.ExecuteAsync(createTableSql);
                }
                else
                {
                    string getColumnsSql = @" SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName";
                    var existingColumns = (await connection.QueryAsync<string>(getColumnsSql, new { TableName = tableName })).Select(c => c.ToLower()).ToHashSet();

                    //find new col 
                    var missingCols = fields
                               .Where(f => !existingColumns.Contains(f))
                               .ToList();
                    if (missingCols.Count > 0)
                    {
                        var alterParts = string.Join(", ", missingCols.Select(col => $"ADD [{col}] NVARCHAR(MAX)"));
                        string alterSql = $"ALTER TABLE [{tableName}] {alterParts}";

                        await connection.ExecuteAsync(alterSql);
                    }




                    //foreach (var field in fields)
                    //{
                    //    if (!existingColumns.Contains(field.ToLower()))
                    //    {
                    //        string alterSql = $"ALTER TABLE [{tableName}] ADD [{field}] NVARCHAR(MAX)";
                    //        await connection.ExecuteAsync(alterSql);
                    //    }
                    //}
                }
                await connection.CloseAsync();
            }
            return fieldNames;
        }

    }
}
