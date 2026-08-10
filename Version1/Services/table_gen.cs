using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Version1.Modal;

namespace SQCScanner.Services
{
    public class table_gen
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        public table_gen(IConfiguration configuration)
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
                string dateCurrent = DateTime.Now.ToString("dd-MM-yyyy/HH:mm:ss");
                string tableName = $"Template_{templateId}";
                string checkTableSql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE @TableName";
                var exists = await connection.QueryFirstOrDefaultAsync(checkTableSql, new { TableName = tableName+"%" });
                bool tableExists = exists != null;
                var fields = respose.FieldResults   
                    .Select((fr, index) => new { Index = index, Key = fr.Key })
                    .OrderBy(fr => fr.Index)
                    .Select(fr => fr.Key)
                    .ToList();
                fieldNames.AddRange(fields);    

                if (!tableExists)
                {
                    tableName = $"{tableName}_{dateCurrent}";
                    var columnsSql = string.Join(", ", fields.Select(f => $"[{f}] NVARCHAR(MAX)"));
                    string createTableSql = $"CREATE TABLE [{tableName}] (Id INT IDENTITY(1,1) PRIMARY KEY, {"[LiveTime] NVARCHAR(MAX)"},{"[UserName] NVARCHAR(MAX)"},{"[Status] NVARCHAR(MAX)"}, {"[Report] NVARCHAR(MAX)"}, {columnsSql})"; 
                    await connection.QueryAsync(createTableSql);
                }
                else
                {
                    tableName = exists.TABLE_NAME;
                    var columnsSql = string.Join(", ", fields.Select(f => $"[{f}] NVARCHAR(MAX)"));
                    string getColumnsSql = @" SELECT COLUMN_NAME  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName";
                    var existingColumns = (await connection.QueryAsync<string>(getColumnsSql, new { TableName = tableName })).Select(c => c).ToHashSet();
                    foreach (var field in fields)
                    {
                        var checkColumnSql = @"SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

                        var columnExists = await connection.QueryFirstOrDefaultAsync<int?>(checkColumnSql, new
                        {
                            TableName = tableName,
                            ColumnName = field
                        });

                        if (!columnExists.HasValue)
                        {
                            var alterTableSql = $"ALTER TABLE [{tableName}] ADD [{field}] NVARCHAR(MAX)";
                            await connection.ExecuteAsync(alterTableSql);
                        }
                    }
                }
                await connection.CloseAsync();
            }
            return fieldNames;
        }

    }
}