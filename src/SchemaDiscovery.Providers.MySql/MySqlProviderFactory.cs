using Microsoft.Extensions.Logging;
using SchemaDiscovery.Abstractions;

namespace SchemaDiscovery.Providers.MySql;

public sealed class MySqlProviderFactory : IDatabaseSchemaProviderFactory
{
    public string ProviderName => "mysql";

    public IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory)
        => new MySqlSchemaProvider(connectionString, loggerFactory);
}
