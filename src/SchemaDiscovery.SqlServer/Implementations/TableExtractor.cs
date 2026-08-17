using System.Text;
using Microsoft.Data.SqlClient;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;
using Serilog;
using SchemaDiscovery.Core.Entities;

namespace SchemaDiscovery.SqlServer.Implementations
{
    public class TableExtractor : ITableExtractor
    {
        private readonly ILogger _logger;

        public TableExtractor(ILogger logger)
        {
            _logger = logger;
        }

        async Task<IEnumerable<Table>> ITableExtractor.GetTables(ExtractionOptions options,
            CancellationToken cancellationToken)
        {
            _logger.Information("Discovering tables...");

            var tables = new List<Table>();

            try
            {
                await using (var connection = new SqlConnection(options.ConnectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    await using var command = new SqlCommand(Constants.SQL_GET_TABLES, connection);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var schema = reader.GetString(reader.GetOrdinal("TABLE_SCHEMA"));
                        var name = reader.GetString(reader.GetOrdinal("TABLE_NAME"));

                        tables.Add(new Table
                        {
                            Schema = schema,
                            Name = name,
                            ClassName = ToPascalCase(name),
                            PluralClassName = Pluralize(ToPascalCase(name)),
                            DisplayName = ToDisplayName(name),
                            PluralDisplayName = Pluralize(ToDisplayName(name))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to discover tables.");
                throw;
            }

            _logger.Information("Found {TableCount} tables.", tables.Count);

            foreach (var table in tables)
            {
                _logger.Debug("Extracting columns for table {TableSchema}.{TableName}...", table.Schema, table.Name);

                var columns = await GetColumns(table, options, cancellationToken);
                table.Columns = columns.ToList();

                _logger.Debug("Found {ColumnCount} columns for table {TableSchema}.{TableName}.",
                    table.Columns.Count, table.Schema, table.Name);
            }

            return tables;
        }

        private async Task<IEnumerable<ColumnInfo>> GetColumns(Table table, ExtractionOptions options, CancellationToken cancellationToken)
        {
            var columns = new List<ColumnInfo>();

            try
            {
                await using var connection = new SqlConnection(options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(Constants.SQL_GET_TABLE_COLUMNS, connection);
                command.Parameters.AddWithValue("@TABLE_SCHEMA", table.Schema);
                command.Parameters.AddWithValue("@TABLE_NAME", table.Name);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var columnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
                    var charMaxLengthOrdinal = reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH");

                    columns.Add(new ColumnInfo
                    {
                        ColumnName = columnName,
                        PropertyName = ToPascalCase(columnName),
                        DisplayName = ToDisplayName(columnName),
                        SqlType = reader.GetString(reader.GetOrdinal("DATA_TYPE")),
                        CharMaxLength = reader.IsDBNull(charMaxLengthOrdinal) ? 0 : reader.GetInt32(charMaxLengthOrdinal),
                        Precision = reader.GetInt32(reader.GetOrdinal("NUMERIC_PRECISION")),
                        Scale = reader.GetInt32(reader.GetOrdinal("NUMERIC_SCALE")),
                        IsNullable = reader.GetString(reader.GetOrdinal("IS_NULLABLE")) == "YES",
                        IsPrimaryKey = reader.GetString(reader.GetOrdinal("IS_PRIMARY_KEY")) == "YES",
                        IsIdentity = reader.GetString(reader.GetOrdinal("IS_IDENTITY")) == "YES"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract columns for table {TableSchema}.{TableName}.", table.Schema, table.Name);
                throw;
            }

            return columns;
        }

        private static string ToPascalCase(string name)
        {
            var parts = name.Split(['_', ' ', '-'], StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var part in parts)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    sb.Append(part[1..]);
                }
            }

            return sb.ToString();
        }

        private static string ToDisplayName(string name)
        {
            var words = name.Split(['_', ' ', '-'], StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var word in words)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word[1..].ToLowerInvariant());
                }
            }

            return sb.ToString();
        }

        private static string Pluralize(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            if (word.EndsWith('y') && word.Length > 1 && !"aeiouAEIOU".Contains(word[^2]))
            {
                return word[..^1] + "ies";
            }

            if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith('z') ||
                word.EndsWith("ch") || word.EndsWith("sh"))
            {
                return word + "es";
            }

            return word + "s";
        }
    }
}
