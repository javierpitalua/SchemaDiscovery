using System.Reflection;
using SchemaDiscovery.Core.Entities;
using SchemaDiscovery.Entities;

namespace SchemaDiscovery.Core.Engine;

public static class Mappings
{
    public static View MapFromExisting(View existing, View target) =>
        DatabaseObjectWithColumnsMapper.MapFromExisting(existing, target);

    public static Table MapFromExisting(Table existing, Table target) =>
        DatabaseObjectWithColumnsMapper.MapFromExisting(existing, target);

    public static Routine MapFromExisting(Routine existing, Routine target)
    {
        PersistedPropertyCopier.CopyPersistedProperties(existing, target);

        var existingParametersByName = existing.Parameters.ToDictionary(p => p.ParameterName);

        foreach (var parameter in target.Parameters)
        {
            if (existingParametersByName.TryGetValue(parameter.ParameterName, out var existingParameter))
            {
                PersistedPropertyCopier.CopyPersistedProperties(existingParameter, parameter);
            }
        }

        var existingReturnedColumnsByName = existing.ReturnedColumns.ToDictionary(c => c.ColumnName);

        foreach (var column in target.ReturnedColumns)
        {
            if (existingReturnedColumnsByName.TryGetValue(column.ColumnName, out var existingColumn))
            {
                PersistedPropertyCopier.CopyPersistedProperties(existingColumn, column);
            }
        }

        return target;
    }
}

internal static class DatabaseObjectWithColumnsMapper
{
    public static T MapFromExisting<T>(T existing, T target) where T : DatabaseObjectWithColumns
    {
        PersistedPropertyCopier.CopyPersistedProperties(existing, target);

        var existingColumnsByName = existing.Columns.ToDictionary(c => c.ColumnName);

        foreach (var column in target.Columns)
        {
            if (existingColumnsByName.TryGetValue(column.ColumnName, out var existingColumn))
            {
                PersistedPropertyCopier.CopyPersistedProperties(existingColumn, column);
            }
        }

        return target;
    }
}



