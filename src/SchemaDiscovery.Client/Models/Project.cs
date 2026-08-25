using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchemaDiscovery.Client.Models
{
    /// <summary>
    /// A scanned database schema, assembled by <see cref="ProjectLoader"/> from the
    /// per-object JSON files a schema-discovery run wrote to an output directory.
    /// </summary>
    public class Project : ProjectInfo
    {
        public List<TableSchema> Tables { get; set; } = new List<TableSchema>();
        public List<ViewSchema> Views { get; set; } = new List<ViewSchema>();
        public List<RoutineSchema> Routines { get; set; } = new List<RoutineSchema>();
    }
}
