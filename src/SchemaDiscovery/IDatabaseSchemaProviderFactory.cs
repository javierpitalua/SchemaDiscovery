using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery;

/// <summary>
/// Creates instances of a specific <see cref="IDatabaseSchemaProvider"/>.
/// Each database implementation ships one of these so the CLI can discover
/// and instantiate it without depending on the concrete provider type.
/// </summary>
public interface IDatabaseSchemaProviderFactory
{
    /// <summary>Must match the value passed to --provider on the command line.</summary>
    string ProviderName { get; }

    IDatabaseSchemaProvider Create(string connectionString, ILoggerFactory loggerFactory);
}
