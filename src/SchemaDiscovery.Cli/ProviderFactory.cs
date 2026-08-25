using Microsoft.Extensions.Logging;
using SchemaDiscovery;

namespace SchemaDiscovery.Cli;

/// <summary>
/// Central registry of known <see cref="IDatabaseSchemaProviderFactory"/> implementations.
/// To add support for a new database engine: implement IDatabaseSchemaProvider and
/// IDatabaseSchemaProviderFactory in a new project (see SchemaDiscovery.Providers.PostgreSql
/// for the expected shape), then register the factory in DependencyResolution/DefaultModule.cs.
/// </summary>
public sealed class ProviderFactory
{
    private readonly Dictionary<string, IDatabaseSchemaProviderFactory> _factories;

    public ProviderFactory(IEnumerable<IDatabaseSchemaProviderFactory> factories)
    {
        _factories = factories.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> SupportedProviders => _factories.Keys;

    public IDatabaseSchemaProvider Create(string providerName, string connectionString, ILoggerFactory loggerFactory, CultureLanguages culture)
    {
        if (!_factories.TryGetValue(providerName, out var factory))
        {
            throw new NotSupportedException(
                $"Provider '{providerName}' is not registered. Supported providers: {string.Join(", ", SupportedProviders)}.");
        }

        return factory.Create(connectionString, loggerFactory, culture);
    }
}
