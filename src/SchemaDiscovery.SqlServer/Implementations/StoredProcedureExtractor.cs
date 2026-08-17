using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.SqlServer.Implementations;

public class StoredProcedureExtractor : IStoredProcedureExtractor
{
    public Task<IEnumerable<StoredProcedure>> GetStoredProcedures(ExtractionOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}