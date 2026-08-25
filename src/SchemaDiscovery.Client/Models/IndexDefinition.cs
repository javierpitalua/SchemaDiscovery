using System.Collections.Generic;

namespace SchemaDiscovery.Client.Models
{
    public class IndexDefinition
    {
        public string Name { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }

        /// <summary>Provider-native index type description (e.g. "CLUSTERED", "btree").</summary>
        public string IndexType { get; set; } = string.Empty;

        public List<string> Columns { get; set; } = new List<string>();
    }
}
