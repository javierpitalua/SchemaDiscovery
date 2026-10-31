using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using SchemaDiscovery;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.PostgreSql;

/// <summary>
/// Scans a PostgreSQL database using the pg_catalog system catalogs. Catalogs
/// are used instead of information_schema because they expose richer metadata
/// (identity/generated columns, index access methods, procedure vs. function
/// kind, parameter modes, full routine definitions, etc.).
/// System schemas (pg_catalog, information_schema, pg_toast*, pg_temp*) and
/// objects owned by extensions are excluded.
/// Requires PostgreSQL 12 or later (generated columns).
/// </summary>
public sealed class PostgreSqlSchemaProvider : IDatabaseSchemaProvider
{
    public string ProviderName => "postgresql";

    // Shared predicate that filters out system schemas. Expects the namespace alias "n".
    private const string UserSchemaFilter = """
        n.nspname NOT IN ('pg_catalog', 'information_schema')
        AND n.nspname NOT LIKE 'pg\_toast%'
        AND n.nspname NOT LIKE 'pg\_temp%'
        """;

    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlSchemaProvider> _logger;
    private readonly IHumanizer _humanizer;
    private readonly CultureLanguages _culture;
    private NpgsqlConnection? _connection;

    public PostgreSqlSchemaProvider(string connectionString, ILoggerFactory loggerFactory, CultureLanguages culture)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger = loggerFactory.CreateLogger<PostgreSqlSchemaProvider>();
        _humanizer = new Humanizer();
        _culture = culture;
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { State: ConnectionState.Open })
            return _connection;

        if (_connection is not null)
            await _connection.DisposeAsync();

        _connection = new NpgsqlConnection(_connectionString);
        _logger.LogDebug("Opening PostgreSQL connection...");
        await _connection.OpenAsync(cancellationToken);
        _logger.LogInformation(
            "Connected to database '{Database}' on server '{Server}'.",
            _connection.Database, _connection.Host);

        return _connection;
    }

    public async Task<IReadOnlyList<TableSchema>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var tables = new List<TableSchema>();

        // relkind: r = ordinary table, p = partitioned table.
        const string tableListSql = $"""
            SELECT n.nspname, c.relname, c.oid
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p')
              AND NOT c.relispartition
              AND {UserSchemaFilter}
              AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = c.oid AND d.deptype = 'e')
            ORDER BY n.nspname, c.relname;
            """;

        var refs = new List<(string Schema, string Name, uint Oid)>();

        await using (var cmd = new NpgsqlCommand(tableListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                refs.Add((reader.GetString(0), reader.GetString(1), reader.GetFieldValue<uint>(2)));
        }

        _logger.LogInformation("Found {Count} table(s).", refs.Count);

        foreach (var tableRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Scanning table {Schema}.{Table}...", tableRef.Schema, tableRef.Name);

            var table = new TableSchema
            {
                Schema = tableRef.Schema,
                ClassName = _humanizer.ToPascalCase(tableRef.Name),
                PluralClassName = _humanizer.ToPluralCase(tableRef.Name, _culture),
                DisplayName = _humanizer.ToHumanReadable(tableRef.Name),
                PluralDisplayName = _humanizer.ToPluralCase(_humanizer.ToHumanReadable(tableRef.Name), _culture),
                Name = tableRef.Name,
            };

            table.Columns.AddRange(await GetColumnsAsync(connection, tableRef.Oid, cancellationToken));
            table.PrimaryKeyColumns.AddRange(await GetPrimaryKeyColumnsAsync(connection, tableRef.Oid, cancellationToken));
            table.ForeignKeys.AddRange(await GetForeignKeysAsync(connection, tableRef.Oid, cancellationToken));
            table.Indexes.AddRange(await GetIndexesAsync(connection, tableRef.Oid, cancellationToken));

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

        // relkind: v = view, m = materialized view.
        const string viewListSql = $"""
            SELECT n.nspname, c.relname, c.oid, pg_get_viewdef(c.oid, true)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('v', 'm')
              AND {UserSchemaFilter}
              AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = c.oid AND d.deptype = 'e')
            ORDER BY n.nspname, c.relname;
            """;

        var refs = new List<(string Schema, string Name, uint Oid, string? Definition)>();

        await using (var cmd = new NpgsqlCommand(viewListSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetFieldValue<uint>(2),
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
                Definition = viewRef.Definition ?? string.Empty
            };

            view.Columns.AddRange(await GetColumnsAsync(connection, viewRef.Oid, cancellationToken));
            views.Add(view);
        }

        return views;
    }

    public Task<IReadOnlyList<RoutineSchema>> GetStoredProceduresAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.StoredProcedure, 'p', cancellationToken);

    public Task<IReadOnlyList<RoutineSchema>> GetFunctionsAsync(CancellationToken cancellationToken = default)
        => GetRoutinesAsync(SchemaObjectType.Function, 'f', cancellationToken);

    private async Task<IReadOnlyList<RoutineSchema>> GetRoutinesAsync(
        SchemaObjectType objectType, char procKind, CancellationToken cancellationToken)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        var routines = new List<RoutineSchema>();

        // prokind: f = function, p = procedure (aggregates and window functions are ignored).
        // Only routines written in a language with readable source get a full definition;
        // pg_get_functiondef works for all of f/p kinds, including C and internal ones.
        const string routineListSql = $"""
            SELECT n.nspname, p.proname, p.oid, pg_get_functiondef(p.oid),
                   CASE WHEN p.prokind = 'f' THEN pg_get_function_result(p.oid) END
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE p.prokind = @kind
              AND {UserSchemaFilter}
              AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = p.oid AND d.deptype = 'e')
            ORDER BY n.nspname, p.proname, p.oid;
            """;

        var refs = new List<(string Schema, string Name, uint Oid, string? Definition, string? ReturnType)>();

        await using (var cmd = new NpgsqlCommand(routineListSql, connection))
        {
            cmd.Parameters.AddWithValue("kind", procKind);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetFieldValue<uint>(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
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
                Definition = routineRef.Definition ?? string.Empty,
                ReturnType = routineRef.ReturnType ?? string.Empty
            };

            var (parameters, resultColumns) = await GetParametersAsync(
                connection, routineRef.Oid, objectType, cancellationToken);
            routine.Parameters.AddRange(parameters);
            routine.ResultColumns.AddRange(resultColumns);

            routines.Add(routine);
        }

        return routines;
    }

    private static async Task<List<ColumnDefinition>> GetColumnsAsync(
        NpgsqlConnection connection, uint relationOid, CancellationToken cancellationToken)
    {
        // atttypmod encodes length/precision/scale:
        //   varchar(n)/char(n): n + 4;  bit(n)/varbit(n): n;
        //   numeric(p,s): ((p << 16) | s) + 4.
        // attidentity: 'a' = ALWAYS, 'd' = BY DEFAULT. attgenerated: 's' = stored generated column.
        // Serial columns have no identity flag but a nextval(...) default, so they are treated as identity too.
        const string sql = """
            SELECT a.attnum,
                   a.attname,
                   format_type(a.atttypid, NULL) AS data_type,
                   NOT a.attnotnull AS is_nullable,
                   CASE WHEN a.atttypmod > 4 AND t.typname IN ('varchar', 'bpchar') THEN a.atttypmod - 4
                        WHEN a.atttypmod > 0 AND t.typname IN ('bit', 'varbit') THEN a.atttypmod
                   END AS max_length,
                   CASE WHEN t.typname = 'numeric' AND a.atttypmod >= 4
                        THEN ((a.atttypmod - 4) >> 16) & 65535 END AS num_precision,
                   CASE WHEN t.typname = 'numeric' AND a.atttypmod >= 4
                        THEN (a.atttypmod - 4) & 65535 END AS num_scale,
                   (a.attidentity <> '' OR COALESCE(pg_get_expr(ad.adbin, ad.adrelid) LIKE 'nextval(%', false)) AS is_identity,
                   (a.attgenerated <> '') AS is_computed,
                   pg_get_expr(ad.adbin, ad.adrelid) AS default_expr
            FROM pg_attribute a
            JOIN pg_type t ON t.oid = a.atttypid
            LEFT JOIN pg_attrdef ad ON ad.adrelid = a.attrelid AND ad.adnum = a.attnum
            WHERE a.attrelid = @oid AND a.attnum > 0 AND NOT a.attisdropped
            ORDER BY a.attnum;
            """;

        var columns = new List<ColumnDefinition>();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlTypes.NpgsqlDbType.Oid) { Value = relationOid });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var isComputed = reader.GetBoolean(8);
            var expression = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);

            columns.Add(new ColumnDefinition
            {
                OrdinalPosition = reader.GetInt16(0),
                Name = reader.GetString(1),
                PropertyName = reader.GetString(1),
                DataType = reader.GetString(2),
                IsNullable = reader.GetBoolean(3),
                MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                NumericPrecision = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                NumericScale = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                IsIdentity = reader.GetBoolean(7),
                IsComputed = isComputed,
                // For generated columns the pg_attrdef expression is the generation expression.
                ComputedExpression = isComputed ? expression : string.Empty,
                DefaultValue = isComputed ? string.Empty : expression
            });
        }

        return columns;
    }

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        NpgsqlConnection connection, uint relationOid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.attname
            FROM pg_constraint con
            CROSS JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
            WHERE con.conrelid = @oid AND con.contype = 'p'
            ORDER BY k.ord;
            """;

        var columns = new List<string>();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlTypes.NpgsqlDbType.Oid) { Value = relationOid });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(0));

        return columns;
    }

    private static async Task<List<ForeignKeyDefinition>> GetForeignKeysAsync(
        NpgsqlConnection connection, uint relationOid, CancellationToken cancellationToken)
    {
        // confdeltype / confupdtype: a = NO ACTION, r = RESTRICT, c = CASCADE, n = SET NULL, d = SET DEFAULT.
        const string sql = """
            SELECT con.conname, a.attname, rn.nspname, rc.relname, ra.attname,
                   CASE con.confdeltype WHEN 'a' THEN 'NO ACTION' WHEN 'r' THEN 'RESTRICT' WHEN 'c' THEN 'CASCADE'
                                        WHEN 'n' THEN 'SET NULL' WHEN 'd' THEN 'SET DEFAULT' END,
                   CASE con.confupdtype WHEN 'a' THEN 'NO ACTION' WHEN 'r' THEN 'RESTRICT' WHEN 'c' THEN 'CASCADE'
                                        WHEN 'n' THEN 'SET NULL' WHEN 'd' THEN 'SET DEFAULT' END
            FROM pg_constraint con
            CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS k(attnum, refattnum, ord)
            JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
            JOIN pg_class rc ON rc.oid = con.confrelid
            JOIN pg_namespace rn ON rn.oid = rc.relnamespace
            JOIN pg_attribute ra ON ra.attrelid = con.confrelid AND ra.attnum = k.refattnum
            WHERE con.conrelid = @oid AND con.contype = 'f'
            ORDER BY con.conname, k.ord;
            """;

        var foreignKeys = new List<ForeignKeyDefinition>();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlTypes.NpgsqlDbType.Oid) { Value = relationOid });

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
                DeleteRule = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                UpdateRule = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }

        return foreignKeys;
    }

    private static async Task<List<IndexDefinition>> GetIndexesAsync(
        NpgsqlConnection connection, uint relationOid, CancellationToken cancellationToken)
    {
        // Only key columns are listed (INCLUDE columns are skipped). pg_get_indexdef with a
        // column number returns the column name, or the expression text for expression indexes.
        const string sql = """
            SELECT ic.relname, i.indisunique, i.indisprimary, am.amname,
                   pg_get_indexdef(i.indexrelid, k.ord::int, true) AS column_name
            FROM pg_index i
            JOIN pg_class ic ON ic.oid = i.indexrelid
            JOIN pg_am am ON am.oid = ic.relam
            CROSS JOIN LATERAL generate_series(1, i.indnkeyatts::int) AS k(ord)
            WHERE i.indrelid = @oid
            ORDER BY ic.relname, k.ord;
            """;

        var indexesByName = new Dictionary<string, IndexDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlTypes.NpgsqlDbType.Oid) { Value = relationOid });

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

    /// <summary>
    /// Reads a routine's arguments from pg_proc. proargmodes: i = IN, o = OUT, b = INOUT,
    /// v = VARIADIC, t = TABLE column. TABLE columns of a function, and OUT/INOUT arguments of
    /// a procedure (PostgreSQL procedures have no result sets; OUT values are returned as a
    /// single row by CALL), are reported as result columns rather than just parameters.
    /// </summary>
    private static async Task<(List<ParameterDefinition> Parameters, List<ColumnDefinition> ResultColumns)> GetParametersAsync(
        NpgsqlConnection connection, uint routineOid, SchemaObjectType objectType, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.ord::int,
                   COALESCE(NULLIF(p.proargnames[a.ord], ''), 'arg' || a.ord) AS arg_name,
                   format_type(a.typ, NULL) AS data_type,
                   COALESCE(p.proargmodes[a.ord]::text, 'i') AS arg_mode
            FROM pg_proc p
            CROSS JOIN LATERAL unnest(COALESCE(p.proallargtypes, p.proargtypes::oid[])) WITH ORDINALITY AS a(typ, ord)
            WHERE p.oid = @oid
            ORDER BY a.ord;
            """;

        var parameters = new List<ParameterDefinition>();
        var resultColumns = new List<ColumnDefinition>();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlTypes.NpgsqlDbType.Oid) { Value = routineOid });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ordinal = reader.GetInt32(0);
            var name = reader.GetString(1);
            var dataType = reader.GetString(2);
            var mode = reader.GetString(3);

            var isResultColumn = mode == "t"
                || (objectType == SchemaObjectType.StoredProcedure && mode is "o" or "b");

            if (isResultColumn)
            {
                resultColumns.Add(new ColumnDefinition
                {
                    OrdinalPosition = resultColumns.Count + 1,
                    Name = name,
                    PropertyName = name,
                    DataType = dataType,
                    IsNullable = true
                });
            }

            if (mode == "t")
                continue; // TABLE columns are part of the return type, not input parameters.

            parameters.Add(new ParameterDefinition
            {
                OrdinalPosition = ordinal,
                Name = name,
                PropertyName = name,
                DataType = dataType,
                Mode = mode switch
                {
                    "o" => "OUT",
                    "b" => "INOUT",
                    _ => "IN" // 'i' and 'v' (variadic)
                }
            });
        }

        return (parameters, resultColumns);
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
