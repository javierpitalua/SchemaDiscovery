using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.SqlServer.Implementations;

public class RoutineExtractor : IStoredProcedureExtractor
{
    public Task<IEnumerable<Routine>> GetStoredProcedures(ExtractionOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<Routine>>(new List<Routine>());
    }
}