using System;
using System.Collections.Generic;
using System.Text;
using SchemaDiscovery.Core.Entities;

namespace SchemaDiscovery.Entities
{
    public class StoredProcedure : DatabaseObject
    {
        public List<StoredProcedureParameter> Parameters { get; set; } = new  List<StoredProcedureParameter>();
        public List<ColumnInfo> ReturnedColumns { get; set; } = new List<ColumnInfo>();
    }
}
