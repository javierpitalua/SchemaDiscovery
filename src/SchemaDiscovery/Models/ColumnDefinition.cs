using SchemaDiscovery.Models.Infrastructure;

namespace SchemaDiscovery.Models
{

    public class ColumnDefinition
    {
        public int OrdinalPosition { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Provider-native type name (e.g. "nvarchar", "int", "numeric").</summary>
        public string DataType { get; set; } = string.Empty;

        public bool IsNullable { get; set; }
        public long? MaxLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }
        public bool IsPrimaryKey { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string ComputedExpression { get; set; } = string.Empty;

        // --- Custom, user-editable metadata -------------------------------
        // Everything above this point is overwritten on every scan. Everything
        // below is [Persist]ed: edit these directly in the exported JSON file
        // and the value survives future `schema-discovery` runs. Columns are
        // matched between scans by Name (see SchemaPersistence), so renaming a
        // column in the database will lose its persisted values.

        /// <summary>Property name to use when generating code for this column.</summary>
        [Persist]
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Free-text documentation for this column.</summary>
        [Persist]
        public string Description { get; set; } = string.Empty;
    }
}
