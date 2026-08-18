using SchemaDiscovery.Entities;
using Newtonsoft.Json;
using SchemaDiscovery.Core.Entities;

namespace SchemaDiscovery.Core.Engine;

public interface IEntityPersister
{
    void Persist(Table table, string outputFolder, CancellationToken cancellationToken);
    void Persist(View view, string outputFolder, CancellationToken cancellationToken);
    void Persist(Routine sp, string outputFolder, CancellationToken cancellationToken);
}

public class EntityPersister : IEntityPersister
{
    public void Persist(Table table, string outputFolder, CancellationToken cancellationToken)
    {
        var entityFileName = $"{table.Schema}.{table.Name}.json";
        var targetFileName = Path.Combine(outputFolder, "/tables", entityFileName);

        if (System.IO.File.Exists(targetFileName))
        {
            var existing = JsonConvert.DeserializeObject<Table>(System.IO.File.ReadAllText(targetFileName));
            if (existing == null) return;
            
            var toSave = Mappings.MapFromExisting(existing, table);
            System.IO.File.WriteAllText(targetFileName, JsonConvert.SerializeObject(toSave, Formatting.Indented));
            return;
        }
        
        File.WriteAllText(targetFileName, JsonConvert.SerializeObject(table, Formatting.Indented));
    }

    public void Persist(View view, string outputFolder, CancellationToken cancellationToken)
    {
        var entityFileName = $"{view.Schema}.{view.Name}.json";
        var targetFileName = Path.Combine(outputFolder, "/views", entityFileName);

        if (System.IO.File.Exists(targetFileName))
        {
            var existing = JsonConvert.DeserializeObject<View>(System.IO.File.ReadAllText(targetFileName));
            var toSave = Mappings.MapFromExisting(existing, view);
            System.IO.File.WriteAllText(targetFileName, JsonConvert.SerializeObject(toSave, Formatting.Indented));
            return;
        }
        
        File.WriteAllText(targetFileName, JsonConvert.SerializeObject(view, Formatting.Indented));
    }

    public void Persist(Routine sp, string outputFolder, CancellationToken cancellationToken)
    {
        var entityFileName = $"{sp.Schema}.{sp.Name}.json";
        var targetFileName = Path.Combine(outputFolder, "/stored-procedures", entityFileName);

        if (System.IO.File.Exists(targetFileName))
        {
            var existing = JsonConvert.DeserializeObject<Routine>(System.IO.File.ReadAllText(targetFileName));
            if (existing == null) return;
            
            var toSave = Mappings.MapFromExisting(existing, sp);
            System.IO.File.WriteAllText(targetFileName, JsonConvert.SerializeObject(toSave, Formatting.Indented));
            return;
        }
        
        File.WriteAllText(targetFileName, JsonConvert.SerializeObject(sp, Formatting.Indented));
    }
}