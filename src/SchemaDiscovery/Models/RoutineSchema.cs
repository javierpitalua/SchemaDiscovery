using System;
using System.Collections.Generic;

namespace SchemaDiscovery.Models
{
    /// <summary>
    /// Represents either a stored procedure or a function, depending on
    /// <see cref="SchemaObjectBase.ObjectType"/>.
    /// </summary>
    public class RoutineSchema : SchemaObjectBase
    {
        public RoutineSchema(SchemaObjectType objectType) : base(objectType)
        {
            if (objectType != SchemaObjectType.StoredProcedure && objectType != SchemaObjectType.Function)
            {
                throw new ArgumentOutOfRangeException(nameof(objectType), objectType,
                    "RoutineSchema only supports StoredProcedure or Function.");
            }
        }

        public List<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();

        /// <summary>
        /// Columns of the routine's first result set (stored procedures only),
        /// when the provider can determine them without executing the routine.
        /// Empty when the shape can't be resolved statically (e.g. dynamic SQL).
        /// </summary>
        public List<ColumnDefinition> ResultColumns { get; set; } = new List<ColumnDefinition>();

        /// <summary>Return type, applicable to functions.</summary>
        public string ReturnType { get; set; } = string.Empty;

        /// <summary>The routine's SQL definition, when accessible.</summary>
        public string Definition { get; set; }
    }
}
