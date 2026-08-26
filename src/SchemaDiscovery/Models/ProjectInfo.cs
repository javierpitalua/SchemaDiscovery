using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Models
{
    public class ProjectInfo
    {
        public string ProviderName { get; set; }
        /// <summary>UTC timestamp of when this object was scanned.</summary>
        public DateTimeOffset ScannedAtUtc { get; set; }

        public string CultureLanguage { get; set; }
    }
}
