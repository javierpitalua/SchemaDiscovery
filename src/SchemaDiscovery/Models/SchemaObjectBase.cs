using System;

namespace SchemaDiscovery.Models
{
    /// <summary>
    /// Common metadata shared by every scanned database object (table, view,
    /// stored procedure, function). This is the base type serialized to JSON.
    /// </summary>
    public abstract class SchemaObjectBase
    {
        protected SchemaObjectBase(SchemaObjectType objectType)
        {
            ObjectType = objectType;
        }

        /// <summary>Schema/namespace the object belongs to (e.g. "dbo", "public").</summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>Object name, without schema prefix.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The kind of object this record represents.</summary>
        public SchemaObjectType ObjectType { get; }

        /// <summary>Convenience "schema.name" representation, not serialized separately.</summary>
        public string QualifiedName => string.IsNullOrWhiteSpace(Schema) ? Name : $"{Schema}.{Name}";
    }
}
