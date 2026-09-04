namespace SchemaDiscovery.Client.Models
{
    public class ParameterDefinition
    {
        public int OrdinalPosition { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;

        /// <summary>"IN", "OUT" or "INOUT".</summary>
        public string Mode { get; set; } = "IN";

        public long? MaxLength { get; set; }
    }
}
