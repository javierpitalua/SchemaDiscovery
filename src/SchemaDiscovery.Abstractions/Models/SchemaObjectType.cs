namespace SchemaDiscovery.Abstractions.Models;

/// <summary>
/// The kind of database object a schema record represents.
/// </summary>
public enum SchemaObjectType
{
    Table,
    View,
    StoredProcedure,
    Function
}
