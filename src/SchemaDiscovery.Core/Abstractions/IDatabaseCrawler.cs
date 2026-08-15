using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Abstractions
{
    public interface ITableExtractor
    {
        Task<IEnumerable<Table>> GetTables(ExtractionOptions options, CancellationToken cancellationToken);
    }

    public interface IViewExtractor
    {
        Task<IEnumerable<View>> GetViews(ExtractionOptions options, CancellationToken cancellationToken);
    }

    public interface IStoredProcedureExtractor
    {
        Task<IEnumerable<StoredProcedure>> GetStoredProcedures(ExtractionOptions options, CancellationToken cancellationToken);
    }

    public interface IDatabaseCrawler : 
            ITableExtractor, IViewExtractor, IStoredProcedureExtractor { }
}
