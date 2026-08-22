using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchemaDiscovery;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.SqlServer;

/// <summary>
/// Scans a SQL Server database using the sys.* catalog views. Catalog views
/// are used instead of INFORMATION_SCHEMA where possible because they expose
/// richer metadata (identity columns, computed columns, index types, etc.).
/// </summary>
public sealed class SqlServerSchemaProvider : IDatabaseSchemaProvider
{
    public string ProviderName => "sqlserver";

    private readonly string _connectionString;
    private readonly ILogger<SqlServerSchemaProvider> _logger;
    private SqlConnection? _connection;

    public SqlServerSchemaProvider(string connectionString, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger = loggerFactory.CreateLogger<SqlServerSchemaProvider>();
    }

    private async Task<SqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { State: ConnectionState.Open })
            return _connection;

        _connection = new SqlConnection(_connectionString);
        _logger.LogDebug("Opening SQL Server connection...");
        await _connection.OpenAsync(cancellationToken);
        _logger.LogInformation(
            "Connected to database '{Database}' on server '{Server}'.",
            _connection.Database, _connection.DataSource);

        return _connection;
    }

    public async Task<IReadOnlyList<TableSchema>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var tables = new List<TableSchema>();

        const string tableListSql = """
            SELECT s.name AS SchemaName, t.name AS TableName, t.object_id AS ObjectId,
                   SUM(p.rows) AS RowCountEstimate
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            GROUP BY s.name, t.name, t.object_id
            ORDER BY s.name, t.name;
            """;

        var refs = new List<(string Schema, string Name, int ObjectId, long? RowCount)>();

        await using (var cmd = new SqlCommand(tableListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3)));
            }
        }

        _logger.LogInformation("Found {Count} table(s).", refs.Count);

        foreach (var tableRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Scanning table {Schema}.{Table}...", tableRef.Schema, tableRef.Name);

            var table = new TableSchema
            {
                Schema = tableRef.Schema,
                ClassName = tableRef.Name,
                PluralClassName = tableRef.Name,
                Name = tableRef.Name,
                DatabaseProvider = ProviderName,
                RowCountEstimate = tableRef.RowCount
            };

            table.Columns.AddRange(await GetColumnsAsync(connection, tableRef.ObjectId, cancellationToken));
            table.PrimaryKeyColumns.AddRange(await GetPrimaryKeyColumnsAsync(connection, tableRef.ObjectId, cancellationToken));
            table.ForeignKeys.AddRange(await GetForeignKeysAsync(connection, tableRef.ObjectId, cancellationToken));
            table.Indexes.AddRange(await GetIndexesAsync(connection, tableRef.ObjectId, cancellationToken));

            foreach (var column in table.Columns)
            {
                column.IsPrimaryKey = table.PrimaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
            }

            tables.Add(table);
        }

        return tables;
    }

    public async Task<IReadOnlyList<ViewSchema>> GetViewsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var views = new List<ViewSchema>();

        const string viewListSql = """
            SELECT s.name AS SchemaName, v.name AS ViewName, v.object_id AS ObjectId, m.definition AS Definition
            FROM sys.views v
            JOIN sys.schemas s ON v.schema_id = s.schema_id
            LEFT JOIN sys.sql_modules m ON m.object_id = v.object_id
            ORDER BY s.name, v.name;
            """;

        var refs = new List<(string Schema, string Name, int ObjectId, string? Definition)>();

        await using (var cmd = new SqlCommand(viewListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        _logger.LogInformation("Found {Count} view(s).", refs.Count);

        foreach (var viewRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Scanning view {Schema}.{View}...", viewRef.Schema, viewRef.Name);

            var view = new ViewSchema
            {
                Schema = viewRef.Schema,
                Name = viewRef.Name,
                DatabaseProvider = ProviderName,
                Definition = viewRef.Definition
            };

            view.Columns.AddRange(await GetColumnsAsync(connection, viewRef.ObjectId, cancellationToken));
            views.Add(view);
        }

        return views;
    }

    public Task<IReadOnlyList<RoutineSchema>> GetStoredProceduresAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.StoredProcedure, new[] { "P" }, cancellationToken);

    public Task<IReadOnlyList<RoutineSchema>> GetFunctionsAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.Function, new[] { "FN", "IF", "TF" }, cancellationToken);

    private async Task<IReadOnlyList<RoutineSchema>> GetRoutinesAsync(
        SchemaObjectType objectType, string[] typeCodes, CancellationToken cancellationToken)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var routines = new List<RoutineSchema>();

        var typeList = string.Join(", ", typeCodes.Select((_, i) => $"@type{i}"));
        var routineListSql = $"""
            SELECT s.name AS SchemaName, o.name AS ObjectName, o.object_id AS ObjectId,
                   o.type AS TypeCode, m.definition AS Definition
            FROM sys.objects o
            JOIN sys.schemas s ON o.schema_id = s.schema_id
            LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
            WHERE o.type IN ({typeList})
            ORDER BY s.name, o.name;
            """;

        var refs = new List<(string Schema, string Name, int ObjectId, string? Definition)>();

        await using (var cmd = new SqlCommand(routineListSql, connection))
        {
            for (var i = 0; i < typeCodes.Length; i++)
                cmd.Parameters.AddWithValue($"@type{i}", typeCodes[i]);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
            }
        }

        var kindLabel = objectType == SchemaObjectType.StoredProcedure ? "stored procedure" : "function";
        _logger.LogInformation("Found {Count} {Kind}(s).", refs.Count, kindLabel);

        foreach (var routineRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Scanning {Kind} {Schema}.{Name}...", kindLabel, routineRef.Schema, routineRef.Name);

            var routine = new RoutineSchema(objectType)
            {
                Schema = routineRef.Schema,
                Name = routineRef.Name,
                DatabaseProvider = ProviderName,
                Definition = routineRef.Definition
            };

            var (parameters, returnType) = await GetParametersAsync(connection, routineRef.ObjectId, cancellationToken);
            routine.Parameters.AddRange(parameters);
            routine.ReturnType = returnType;

            routines.Add(routine);
        }

        return routines;
    }

    private static async Task<List<ColumnDefinition>> GetColumnsAsync(
        SqlConnection connection, int objectId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.column_id, c.name, ty.name AS DataType, c.is_nullable,
                   c.max_length, c.precision, c.scale,
                   c.is_identity, cc.definition AS ComputedDefinition, dc.definition AS DefaultDefinition
            FROM sys.columns c
            JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE c.object_id = @ObjectId
            ORDER BY c.column_id;
            """;

        var columns = new List<ColumnDefinition>();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dataType = reader.GetString(2);
            var rawMaxLength = reader.GetInt16(4);

            long? maxLength = rawMaxLength switch
            {
                -1 => null, // MAX types (nvarchar(max), varbinary(max), ...)
                _ when IsDoubleByteType(dataType) => rawMaxLength / 2,
                _ => rawMaxLength
            };

            columns.Add(new ColumnDefinition
            {
                OrdinalPosition = reader.GetInt32(0),
                Name = reader.GetString(1),
                PropertyName = reader.GetString(1),
                DataType = dataType,
                IsNullable = reader.GetBoolean(3),
                MaxLength = maxLength,
                NumericPrecision = dataType is "decimal" or "numeric" ? reader.GetByte(5) : null,
                NumericScale = dataType is "decimal" or "numeric" ? reader.GetByte(6) : null,
                IsIdentity = reader.GetBoolean(7),
                IsComputed = !reader.IsDBNull(8),
                ComputedExpression = reader.IsDBNull(8) ? null : reader.GetString(8),
                DefaultValue = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        return columns;
    }

    private static bool IsDoubleByteType(string dataType) =>
        dataType is "nchar" or "nvarchar";

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        SqlConnection connection, int objectId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = @ObjectId AND i.is_primary_key = 1
            ORDER BY ic.key_ordinal;
            """;

        var columns = new List<string>();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(0));

        return columns;
    }

    private static async Task<List<ForeignKeyDefinition>> GetForeignKeysAsync(
        SqlConnection connection, int objectId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT fk.name AS FkName, c.name AS ColumnName,
                   rs.name AS RefSchema, rt.name AS RefTable, rc.name AS RefColumn,
                   fk.delete_referential_action_desc, fk.update_referential_action_desc
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
            JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = @ObjectId
            ORDER BY fk.name, fkc.constraint_column_id;
            """;

        var foreignKeys = new List<ForeignKeyDefinition>();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            foreignKeys.Add(new ForeignKeyDefinition
            {
                Name = reader.GetString(0),
                Column = reader.GetString(1),
                ReferencedSchema = reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumn = reader.GetString(4),
                DeleteRule = reader.IsDBNull(5) ? null : reader.GetString(5),
                UpdateRule = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return foreignKeys;
    }

    private static async Task<List<IndexDefinition>> GetIndexesAsync(
        SqlConnection connection, int objectId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT i.name AS IndexName, i.is_unique, i.is_primary_key, i.type_desc,
                   c.name AS ColumnName, ic.key_ordinal
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = @ObjectId AND i.name IS NOT NULL AND ic.is_included_column = 0
            ORDER BY i.name, ic.key_ordinal;
            """;

        var indexesByName = new Dictionary<string, IndexDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);

            if (!indexesByName.TryGetValue(name, out var index))
            {
                index = new IndexDefinition
                {
                    Name = name,
                    IsUnique = reader.GetBoolean(1),
                    IsPrimaryKey = reader.GetBoolean(2),
                    IndexType = reader.GetString(3)
                };
                indexesByName[name] = index;
            }

            index.Columns.Add(reader.GetString(4));
        }

        return indexesByName.Values.ToList();
    }

    private static async Task<(List<ParameterDefinition> Parameters, string? ReturnType)> GetParametersAsync(
        SqlConnection connection, int objectId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.parameter_id, p.name, ty.name AS DataType, p.max_length, p.is_output
            FROM sys.parameters p
            JOIN sys.types ty ON p.user_type_id = ty.user_type_id
            WHERE p.object_id = @ObjectId
            ORDER BY p.parameter_id;
            """;

        var parameters = new List<ParameterDefinition>();
        string? returnType = null;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var parameterId = reader.GetInt32(0);
            var dataType = reader.GetString(2);
            var isOutput = reader.GetBoolean(4);
            var rawMaxLength = reader.GetInt16(3);
            long? maxLength = rawMaxLength switch
            {
                -1 => null,
                _ when IsDoubleByteType(dataType) => rawMaxLength / 2,
                _ => rawMaxLength
            };

            // parameter_id 0 with an empty name represents a function's return value.
            if (parameterId == 0)
            {
                returnType = dataType;
                continue;
            }

            parameters.Add(new ParameterDefinition
            {
                OrdinalPosition = parameterId,
                Name = reader.GetString(1),
                DataType = dataType,
                Mode = isOutput ? "OUT" : "IN",
                MaxLength = maxLength
            });
        }

        return (parameters, returnType);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
