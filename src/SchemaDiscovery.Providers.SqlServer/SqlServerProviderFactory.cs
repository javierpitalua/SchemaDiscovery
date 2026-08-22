using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.SqlServer;

public sealed class SqlServerProviderFactory : IDatabaseSchemaProviderFactory
{
    public string ProviderName => "sqlserver";

    public IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory)
        => new SqlServerSchemaProvider(connectionString, loggerFactory);
}
