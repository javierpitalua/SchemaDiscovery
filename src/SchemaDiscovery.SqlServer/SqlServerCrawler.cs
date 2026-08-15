using SchemaDiscovery.Abstractions;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.SqlServer
{
    public class SqlServerCrawler : IDatabaseCrawler
    {
        

        Task<IEnumerable<StoredProcedure>> IStoredProcedureExtractor.GetStoredProcedures(ExtractionOptions options, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<Table>> ITableExtractor.GetTables(ExtractionOptions options, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<View>> IViewExtractor.GetViews(ExtractionOptions options, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
