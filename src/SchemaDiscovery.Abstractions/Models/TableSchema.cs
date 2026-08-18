namespace SchemaDiscovery.Abstractions.Models;

using SchemaDiscovery.Abstractions;

public class TableSchema : SchemaObjectBase
{
    public TableSchema() : base(SchemaObjectType.Table)
    {
    }

    public List<ColumnDefinition> Columns { get; set; } = new();
    public List<string> PrimaryKeyColumns { get; set; } = new();
    public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();
    public List<IndexDefinition> Indexes { get; set; } = new();

    /// <summary>Approximate row count, when the provider can obtain it cheaply.</summary>
    public long? RowCountEstimate { get; set; }

    // --- Custom, user-editable metadata -------------------------------
    // Everything above this point is overwritten on every scan. Everything
    // below is [Persist]ed: edit these directly in the exported JSON file
    // and the value survives future `schema-discovery` runs. See
    // SchemaPersistence for how that's implemented.

    /// <summary>Class name to use when generating code for this table.</summary>
    [Persist]
    public string? ClassName { get; set; }

    /// <summary>Pluralized class name, e.g. for a DbSet or repository name.</summary>
    [Persist]
    public string? PluralClassName { get; set; }
}
