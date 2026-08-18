using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.SqlServer.Implementations;

public class ViewExtractor : IViewExtractor 
{
    public Task<IEnumerable<View>> GetViews(ExtractionOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<View>>(new List<View>());
    }
}