namespace SchemaDiscovery.Abstractions.Models;

public class ForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
    public string? DeleteRule { get; set; }
    public string? UpdateRule { get; set; }
}
