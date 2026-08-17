using SchemaDiscovery.Core.Engine;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.Core.Entities;

public abstract class DatabaseObjectWithColumns : DatabaseObject
{
    [Persist] public string ClassName { get; set; }

    [Persist] public string PluralClassName { get; set; }

    [Persist] public string DisplayName { get; set; }

    [Persist] public string PluralDisplayName { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
}