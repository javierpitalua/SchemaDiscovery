namespace SchemaDiscovery.Abstractions;

/// <summary>
/// Marks a property as user-editable metadata rather than data sourced from
/// the database. Scanned properties (column type, indexes, etc.) are always
/// overwritten on every run — that's the point of a scanner. Properties
/// marked <see cref="PersistAttribute"/> are the opposite: they hold values
/// a person typed into the exported JSON file (e.g. a generated class name),
/// and <see cref="SchemaPersistence"/> carries them forward from the
/// previous JSON file so a schema refresh never wipes them out.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersistAttribute : Attribute
{
}
