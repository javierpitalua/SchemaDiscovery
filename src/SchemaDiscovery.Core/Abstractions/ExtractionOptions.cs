using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Core.Abstractions
{
    public class ExtractionOptions
    {
        public string ConnectionString { get; set; }
        public string DatabaseType { get; set; }
        public string OutputPath { get; set; }
    }
}
