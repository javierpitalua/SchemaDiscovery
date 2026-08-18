using SchemaDiscovery.Core.Engine;
using SchemaDiscovery.Core.Entities;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.Core.Abstractions
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
        Task<IEnumerable<Routine>> GetStoredProcedures(ExtractionOptions options,
            CancellationToken cancellationToken);
    }

    public interface IDatabaseCrawler :
        ITableExtractor, IViewExtractor, IStoredProcedureExtractor
    {
        DatabaseType ProviderType { get; }
    }
}