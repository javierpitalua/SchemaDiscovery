using System.Collections.Generic;

namespace SchemaDiscovery.Models
{
    public class ViewSchema : SchemaObjectBase
    {
        public ViewSchema() : base(SchemaObjectType.View)
        {
        }

        public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();

        /// <summary>The view's SQL definition, when accessible.</summary>
        public string Definition { get; set; } = string.Empty;
    }
}
