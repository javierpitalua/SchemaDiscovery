using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Entities
{
    public abstract class DatabaseObject
    {
        public string Schema { get; set; }
        public string Name { get; set; }
    }
}
