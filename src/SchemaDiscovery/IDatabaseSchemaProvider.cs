using SchemaDiscovery.Models;

namespace SchemaDiscovery;

/// <summary>
/// Implemented by each database engine (SQL Server, PostgreSQL, MySQL, ...).
/// A provider knows how to connect to one database and enumerate its objects.
/// </summary>
public interface IDatabaseSchemaProvider : IAsyncDisposable
{
    /// <summary>Short, lowercase identifier for this provider (e.g. "sqlserver").</summary>
    string ProviderName { get; }

    Task<IReadOnlyList<TableSchema>> GetTablesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViewSchema>> GetViewsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoutineSchema>> GetStoredProceduresAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoutineSchema>> GetFunctionsAsync(CancellationToken cancellationToken = default);
}
