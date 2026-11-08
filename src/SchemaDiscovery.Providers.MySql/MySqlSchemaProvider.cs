using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.MySql;

/// <summary>
/// Scans a MySQL (or MariaDB) database using information_schema. In MySQL a
/// "schema" and a "database" are the same thing, so discovery is scoped to the
/// database named in the connection string and <see cref="SchemaObjectBase.Schema"/>
/// is set to that database name.
/// </summary>
public sealed class MySqlSchemaProvider : IDatabaseSchemaProvider
{
    public string ProviderName => "mysql";

    private readonly string _connectionString;
    private readonly ILogger<MySqlSchemaProvider> _logger;
    private readonly IHumanizer _humanizer;
    private readonly CultureLanguages _culture;
    private MySqlConnection? _connection;

    public MySqlSchemaProvider(string connectionString, ILoggerFactory loggerFactory, CultureLanguages culture)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger = loggerFactory.CreateLogger<MySqlSchemaProvider>();
        _humanizer = new Humanizer();
        _culture = culture;
    }

    private async Task<MySqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { State: ConnectionState.Open })
            return _connection;

        _connection = new MySqlConnection(_connectionString);
        _logger.LogDebug("Opening MySQL connection...");
        await _connection.OpenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_connection.Database))
        {
            throw new InvalidOperationException(
                "The MySQL connection string must specify a database (e.g. \"Database=mydb\").");
        }

        _logger.LogInformation(
            "Connected to database '{Database}' on server '{Server}'.",
            _connection.Database, _connection.DataSource);

        return _connection;
    }

    // information_schema text columns can surface as strings or byte[] depending
    // on server version/collation, so read everything through these helpers.
    private static string? Str(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static long? Int64(MySqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt64(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public async Task<IReadOnlyList<TableSchema>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var tables = new List<TableSchema>();

        const string tableListSql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_ROWS
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME;
            """;

        var refs = new List<(string Schema, string Name)>();

        await using (var cmd = new MySqlCommand(tableListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                refs.Add((Str(reader, 0)!, Str(reader, 1)!));
        }

        _logger.LogInformation("Found {Count} table(s).", refs.Count);

        foreach (var tableRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Scanning table {Schema}.{Table}...", tableRef.Schema, tableRef.Name);

            var className = _humanizer.ToPascalCase(tableRef.Name);

            var table = new TableSchema
            {
                Schema = tableRef.Schema,
                ClassName = className,
                PluralClassName = _humanizer.ToPluralCase(className, _culture),
                DisplayName = _humanizer.ToHumanReadable(tableRef.Name),
                PluralDisplayName = _humanizer.ToPluralCase(_humanizer.ToHumanReadable(tableRef.Name), _culture),
                Name = tableRef.Name,
            };

            table.Columns.AddRange(await GetColumnsAsync(connection, tableRef.Schema, tableRef.Name, cancellationToken));
            table.PrimaryKeyColumns.AddRange(await GetPrimaryKeyColumnsAsync(connection, tableRef.Schema, tableRef.Name, cancellationToken));
            table.ForeignKeys.AddRange(await GetForeignKeysAsync(connection, tableRef.Schema, tableRef.Name, cancellationToken));
            table.Indexes.AddRange(await GetIndexesAsync(connection, tableRef.Schema, tableRef.Name, cancellationToken));

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
            SELECT TABLE_SCHEMA, TABLE_NAME, VIEW_DEFINITION
            FROM information_schema.VIEWS
            WHERE TABLE_SCHEMA = DATABASE()
            ORDER BY TABLE_NAME;
            """;

        var refs = new List<(string Schema, string Name, string? Definition)>();

        await using (var cmd = new MySqlCommand(viewListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                refs.Add((Str(reader, 0)!, Str(reader, 1)!, Str(reader, 2)));
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
                Definition = viewRef.Definition
            };

            view.Columns.AddRange(await GetColumnsAsync(connection, viewRef.Schema, viewRef.Name, cancellationToken));
            views.Add(view);
        }

        return views;
    }

    public Task<IReadOnlyList<RoutineSchema>> GetStoredProceduresAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.StoredProcedure, "PROCEDURE", cancellationToken);

    public Task<IReadOnlyList<RoutineSchema>> GetFunctionsAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.Function, "FUNCTION", cancellationToken);

    private async Task<IReadOnlyList<RoutineSchema>> GetRoutinesAsync(
        SchemaObjectType objectType, string routineType, CancellationToken cancellationToken)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var routines = new List<RoutineSchema>();

        const string routineListSql = """
            SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_DEFINITION, DATA_TYPE
            FROM information_schema.ROUTINES
            WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = @RoutineType
            ORDER BY ROUTINE_NAME;
            """;

        var refs = new List<(string Schema, string Name, string? Definition, string? ReturnType)>();

        await using (var cmd = new MySqlCommand(routineListSql, connection))
        {
            cmd.Parameters.AddWithValue("@RoutineType", routineType);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                refs.Add((Str(reader, 0)!, Str(reader, 1)!, Str(reader, 2), Str(reader, 3)));
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
                Definition = routineRef.Definition
            };

            routine.Parameters.AddRange(
                await GetParametersAsync(connection, routineRef.Schema, routineRef.Name, routineType, cancellationToken));

            if (objectType == SchemaObjectType.Function)
                routine.ReturnType = routineRef.ReturnType;

            // MySQL offers no static way to describe a stored procedure's result
            // set (it is only known at execution time), so ResultColumns stays empty.

            routines.Add(routine);
        }

        return routines;
    }

    private static async Task<List<ColumnDefinition>> GetColumnsAsync(
        MySqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                   CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                   EXTRA, COLUMN_DEFAULT, GENERATION_EXPRESSION
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table
            ORDER BY ORDINAL_POSITION;
            """;

        var columns = new List<ColumnDefinition>();

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = Str(reader, 1)!;
            var dataType = Str(reader, 2)!;
            var extra = Str(reader, 7) ?? string.Empty;
            var generationExpression = Str(reader, 9);
            var isComputed = extra.Contains("GENERATED", StringComparison.OrdinalIgnoreCase)
                             && !string.IsNullOrEmpty(generationExpression);

            // Types without a fixed precision/scale report NULL here.
            var precision = Int64(reader, 5);
            var scale = Int64(reader, 6);

            columns.Add(new ColumnDefinition
            {
                OrdinalPosition = (int)(Int64(reader, 0) ?? 0),
                Name = name,
                PropertyName = name,
                DataType = dataType,
                IsNullable = string.Equals(Str(reader, 3), "YES", StringComparison.OrdinalIgnoreCase),
                MaxLength = Int64(reader, 4),
                NumericPrecision = precision is null ? null : (int)precision,
                NumericScale = scale is null ? null : (int)scale,
                IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                IsComputed = isComputed,
                ComputedExpression = isComputed ? generationExpression! : string.Empty,
                DefaultValue = Str(reader, 8) ?? string.Empty
            });
        }

        return columns;
    }

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        MySqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table AND CONSTRAINT_NAME = 'PRIMARY'
            ORDER BY ORDINAL_POSITION;
            """;

        var columns = new List<string>();

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(Str(reader, 0)!);

        return columns;
    }

    private static async Task<List<ForeignKeyDefinition>> GetForeignKeysAsync(
        MySqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT kcu.CONSTRAINT_NAME, kcu.COLUMN_NAME,
                   kcu.REFERENCED_TABLE_SCHEMA, kcu.REFERENCED_TABLE_NAME, kcu.REFERENCED_COLUMN_NAME,
                   rc.DELETE_RULE, rc.UPDATE_RULE
            FROM information_schema.KEY_COLUMN_USAGE kcu
            JOIN information_schema.REFERENTIAL_CONSTRAINTS rc
              ON rc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
             AND rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
             AND rc.TABLE_NAME = kcu.TABLE_NAME
            WHERE kcu.TABLE_SCHEMA = @Schema AND kcu.TABLE_NAME = @Table
              AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
            ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION;
            """;

        var foreignKeys = new List<ForeignKeyDefinition>();

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            foreignKeys.Add(new ForeignKeyDefinition
            {
                Name = Str(reader, 0)!,
                Column = Str(reader, 1)!,
                ReferencedSchema = Str(reader, 2)!,
                ReferencedTable = Str(reader, 3)!,
                ReferencedColumn = Str(reader, 4)!,
                DeleteRule = Str(reader, 5),
                UpdateRule = Str(reader, 6)
            });
        }

        return foreignKeys;
    }

    private static async Task<List<IndexDefinition>> GetIndexesAsync(
        MySqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT INDEX_NAME, NON_UNIQUE, INDEX_TYPE, COLUMN_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table
            ORDER BY INDEX_NAME, SEQ_IN_INDEX;
            """;

        var indexesByName = new Dictionary<string, IndexDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = Str(reader, 0)!;

            if (!indexesByName.TryGetValue(name, out var index))
            {
                index = new IndexDefinition
                {
                    Name = name,
                    IsUnique = Int64(reader, 1) == 0,
                    IsPrimaryKey = string.Equals(name, "PRIMARY", StringComparison.OrdinalIgnoreCase),
                    IndexType = Str(reader, 2) ?? string.Empty
                };
                indexesByName[name] = index;
            }

            // Functional (expression) indexes have no COLUMN_NAME.
            var column = Str(reader, 3);
            if (column is not null)
                index.Columns.Add(column);
        }

        return indexesByName.Values.ToList();
    }

    private static async Task<List<ParameterDefinition>> GetParametersAsync(
        MySqlConnection connection, string schema, string routineName, string routineType,
        CancellationToken cancellationToken)
    {
        // ORDINAL_POSITION 0 is a function's return value, which is surfaced via
        // ROUTINES.DATA_TYPE instead, so it is excluded here.
        const string sql = """
            SELECT ORDINAL_POSITION, PARAMETER_NAME, DATA_TYPE, PARAMETER_MODE, CHARACTER_MAXIMUM_LENGTH
            FROM information_schema.PARAMETERS
            WHERE SPECIFIC_SCHEMA = @Schema AND SPECIFIC_NAME = @Name
              AND ROUTINE_TYPE = @RoutineType AND ORDINAL_POSITION > 0
            ORDER BY ORDINAL_POSITION;
            """;

        var parameters = new List<ParameterDefinition>();

        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Name", routineName);
        cmd.Parameters.AddWithValue("@RoutineType", routineType);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = Str(reader, 1) ?? string.Empty;

            parameters.Add(new ParameterDefinition
            {
                OrdinalPosition = (int)(Int64(reader, 0) ?? 0),
                Name = name,
                PropertyName = name,
                DataType = Str(reader, 2) ?? string.Empty,
                Mode = Str(reader, 3) ?? "IN",
                MaxLength = Int64(reader, 4)
            });
        }

        return parameters;
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
