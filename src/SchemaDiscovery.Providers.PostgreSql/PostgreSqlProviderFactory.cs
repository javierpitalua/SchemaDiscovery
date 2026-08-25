using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.PostgreSql;

public sealed class PostgreSqlProviderFactory : IDatabaseSchemaProviderFactory
{
    public string ProviderName => "postgres";

    public IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory, CultureLanguages culture)
        => new PostgreSqlSchemaProvider(connectionString, loggerFactory, culture);

    
}
