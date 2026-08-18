using Microsoft.Extensions.Logging;
using SchemaDiscovery.Abstractions;
using SchemaDiscovery.Abstractions.Models;

namespace SchemaDiscovery.Providers.PostgreSql;

/// <summary>
/// Placeholder PostgreSQL provider. This compiles and registers correctly so
/// the CLI recognizes "-p postgres", but each method throws until implemented.
///
/// To implement:
///  1. Add the Npgsql package reference (see the .csproj).
///  2. Open a connection with NpgsqlConnection.
///  3. Query information_schema.tables / columns for the basics, and
///     pg_catalog (pg_index, pg_constraint, pg_indexes, pg_proc) for
///     primary keys, foreign keys, indexes and routine definitions.
///  4. Follow the same shape as SchemaDiscovery.Providers.SqlServer.SqlServerSchemaProvider.
/// </summary>
public sealed class PostgreSqlSchemaProvider : IDatabaseSchemaProvider
{
    private const string NotImplementedMessage =
        "The PostgreSQL provider is not yet implemented. See PostgreSqlSchemaProvider.cs for extension instructions.";

    public string ProviderName => "postgres";

    private readonly ILogger<PostgreSqlSchemaProvider> _logger;

    public PostgreSqlSchemaProvider(string connectionString, ILoggerFactory loggerFactory)
    {
        _ = connectionString; // will be used once Npgsql is wired up
        _logger = loggerFactory.CreateLogger<PostgreSqlSchemaProvider>();
    }

    public Task<IReadOnlyList<TableSchema>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogError(NotImplementedMessage);
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<IReadOnlyList<ViewSchema>> GetViewsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException(NotImplementedMessage);

    public Task<IReadOnlyList<RoutineSchema>> GetStoredProceduresAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException(NotImplementedMessage);

    public Task<IReadOnlyList<RoutineSchema>> GetFunctionsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException(NotImplementedMessage);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
