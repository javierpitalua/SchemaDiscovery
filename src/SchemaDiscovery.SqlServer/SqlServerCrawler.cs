using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using SchemaDiscovery.Core.Engine;
using SchemaDiscovery.Core.Entities;

namespace SchemaDiscovery.SqlServer
{
    public class SqlServerCrawler : IDatabaseCrawler
    {
        public DatabaseType ProviderType
        {
            get { return DatabaseType.SqlServer; }
        }

        private readonly IStoredProcedureExtractor _storedProcedureExtractor;
        private readonly ITableExtractor _tableExtractor;
        private readonly IViewExtractor _viewExtractor;

        public SqlServerCrawler(IStoredProcedureExtractor storedProcedureExtractor, ITableExtractor tableExtractor,
            IViewExtractor viewExtractor)
        {
            _storedProcedureExtractor = storedProcedureExtractor;
            _tableExtractor = tableExtractor;
            _viewExtractor = viewExtractor;
        }

        async Task<IEnumerable<Table>> ITableExtractor.GetTables(ExtractionOptions options,
            CancellationToken cancellationToken)
        {
            return await _tableExtractor.GetTables(options, cancellationToken);
        }

        async Task<IEnumerable<Routine>> IStoredProcedureExtractor.GetStoredProcedures(
            ExtractionOptions options, CancellationToken cancellationToken)
        {
            return await _storedProcedureExtractor.GetStoredProcedures(options, cancellationToken);
        }

        Task<IEnumerable<View>> IViewExtractor.GetViews(ExtractionOptions options, CancellationToken cancellationToken)
        {
            return _viewExtractor.GetViews(options, cancellationToken);
        }
    }
}