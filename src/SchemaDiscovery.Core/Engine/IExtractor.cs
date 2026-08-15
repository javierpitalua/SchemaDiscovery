using SchemaDiscovery.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Core.Engine
{
    public enum DatabaseType
    {
        SqlServer,
        MySql,
        PostgreSql,
        Oracle,
        undefined
    }

    public interface IExtractor
    {
        Task ExtractAsync(ExtractionOptions options, CancellationToken cancellationToken);
    }
}
