using SchemaDiscovery.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using SchemaDiscovery.Entities;
using Serilog;

namespace SchemaDiscovery.Core.Engine
{
    public enum DatabaseType
    {
        SqlServer,
        MySql,
        PostgreSql,
        undefined
    }

    public interface IExtractor
    {
        Task ExtractAsync(ExtractionOptions options, CancellationToken cancellationToken);
    }

    public class Extractor : IExtractor
    {
        private readonly ILogger _logger;
        private readonly IDatabaseCrawler [] _crawlers;
        private readonly IEntityPersister _persister;

        public Extractor(ILogger logger, IDatabaseCrawler[] crawlers, IEntityPersister persister)
        {
            _logger = logger;
            _crawlers = crawlers;
            _persister = persister;
        }

        public async Task ExtractAsync(ExtractionOptions options, CancellationToken cancellationToken)
        {
            if (options.DatabaseType == DatabaseType.undefined)
            {
                throw new Exception($"Unable to resolve the provided database type.");
            }

            var selectedProvider = _crawlers.SingleOrDefault(x => 
                    x.ProviderType == options.DatabaseType);

            if (selectedProvider == null)
            {
                 throw new Exception($"No provider found for database [{options.DatabaseType}]. Ending process now.");
            }

            var tables = await selectedProvider.GetTables(options, cancellationToken);
            foreach (var table in tables)
            {
                _persister.Persist(table, options.OutputPath, cancellationToken);
            }
            
            var views = await selectedProvider.GetViews(options, cancellationToken);
            foreach (var view in views)
            {
                _persister.Persist(view, options.OutputPath, cancellationToken);
            }
            
            var sps = await selectedProvider.GetStoredProcedures(options, cancellationToken);
            foreach (var sp in sps)
            {
                _persister.Persist(sp, options.OutputPath, cancellationToken);
            }
        }
    }
}
