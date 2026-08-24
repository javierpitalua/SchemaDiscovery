using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Providers.MySql;

public sealed class MySqlProviderFactory : IDatabaseSchemaProviderFactory
{
    public string ProviderName => "mysql";

    public IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory, CultureLanguages culture)
        => new MySqlSchemaProvider(connectionString, loggerFactory, culture);
}
