using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.MySql;

/// <summary>
/// Placeholder MySQL provider. This compiles and registers correctly so the
/// CLI recognizes "-p mysql", but each method throws until implemented.
///
/// To implement:
///  1. Add the MySqlConnector package reference (see the .csproj).
///  2. Open a connection with MySqlConnection.
///  3. Query information_schema.tables / columns / statistics / key_column_usage
///     / referential_constraints / routines, scoped to DATABASE().
///  4. Follow the same shape as SchemaDiscovery.Providers.SqlServer.SqlServerSchemaProvider.
/// </summary>
public sealed class MySqlSchemaProvider : IDatabaseSchemaProvider
{
    private const string NotImplementedMessage =
        "The MySQL provider is not yet implemented. See MySqlSchemaProvider.cs for extension instructions.";

    public string ProviderName => "mysql";

    private readonly ILogger<MySqlSchemaProvider> _logger;

    public MySqlSchemaProvider(string connectionString, ILoggerFactory loggerFactory)
    {
        _ = connectionString; // will be used once MySqlConnector is wired up
        _logger = loggerFactory.CreateLogger<MySqlSchemaProvider>();
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
