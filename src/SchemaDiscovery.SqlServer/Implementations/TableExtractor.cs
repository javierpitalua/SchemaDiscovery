using SchemaDiscovery.Abstractions;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Entities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.SqlServer.Implementations
{
    public class TableExtractor : ITableExtractor
    {
        private readonly ILogger _logger;

        public TableExtractor(ILogger logger)
        {
            _logger = logger;
        }

        Task<IEnumerable<Table>> ITableExtractor.GetTables(ExtractionOptions options, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
