using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SchemaDiscovery.Abstractions.Models;

namespace SchemaDiscovery.Client.Models
{
    public class Project
    {
        public List<TableSchema> Tables { get; set; } = new List<TableSchema>();
        public List<ViewSchema> Views { get; set; } = new List<ViewSchema>();
        public List<RoutineSchema> Routines { get; set; } = new List<RoutineSchema>();
    }
}
