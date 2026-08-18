using Microsoft.Extensions.Logging;
using SchemaDiscovery.Abstractions;
using SchemaDiscovery.Providers.MySql;
using SchemaDiscovery.Providers.PostgreSql;
using SchemaDiscovery.Providers.SqlServer;

namespace SchemaDiscovery.Cli;

/// <summary>
/// Central registry of known <see cref="IDatabaseSchemaProviderFactory"/> implementations.
/// To add support for a new database engine: implement IDatabaseSchemaProvider and
/// IDatabaseSchemaProviderFactory in a new project (see SchemaDiscovery.Providers.PostgreSql
/// for the expected shape), then add an instance of your factory to the array below.
/// </summary>
public sealed class ProviderFactory
{
    private readonly Dictionary<string, IDatabaseSchemaProviderFactory> _factories;

    public ProviderFactory()
    {
        IDatabaseSchemaProviderFactory[] known =
        [
            new SqlServerProviderFactory(),
            new PostgreSqlProviderFactory(),
            new MySqlProviderFactory()
        ];

        _factories = known.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> SupportedProviders => _factories.Keys;

    public IDatabaseSchemaProvider Create(string providerName, string connectionString, ILoggerFactory loggerFactory)
    {
        if (!_factories.TryGetValue(providerName, out var factory))
        {
            throw new NotSupportedException(
                $"Provider '{providerName}' is not registered. Supported providers: {string.Join(", ", SupportedProviders)}.");
        }

        return factory.Create(connectionString, loggerFactory);
    }
}
