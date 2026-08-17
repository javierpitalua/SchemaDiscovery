using System;
using System.Collections.Generic;
using System.Text;
using SchemaDiscovery.Core.Engine;

namespace SchemaDiscovery.Core.Abstractions
{
    public class ExtractionOptions
    {
        public string ConnectionString { get; set; }
        public DatabaseType DatabaseType { get; set; }
        public string OutputPath { get; set; }
    }
}
