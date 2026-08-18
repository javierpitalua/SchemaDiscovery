using Microsoft.Extensions.Logging;
using SchemaDiscovery.Abstractions;

namespace SchemaDiscovery.Providers.PostgreSql;

public sealed class PostgreSqlProviderFactory : IDatabaseSchemaProviderFactory
{
    public string ProviderName => "postgres";

    public IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory)
        => new PostgreSqlSchemaProvider(connectionString, loggerFactory);
}
