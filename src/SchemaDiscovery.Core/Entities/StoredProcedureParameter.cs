using SchemaDiscovery.Core.Engine;

namespace SchemaDiscovery.Core.Entities;

public class StoredProcedureParameter
{
    public string ParameterName { get; set; }
    [Persist]
    public string PropertyName { get; set; }
        
    [Persist]
    public string DisplayName { get; set; }
        
    public string SqlType { get; set; }
    public int CharMaxLength { get; set; }
    public int Precision { get; set; } = 18;
    public int Scale { get; set; } = 2;
    public bool IsNullable { get; set; }
    
}