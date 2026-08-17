using System.Reflection;

namespace SchemaDiscovery.Core.Engine;

public class PersistAttribute : Attribute { }

internal static class PersistedPropertyCopier
{
    public static void CopyPersistedProperties<T>(T source, T target)
    {
        var persistedProperties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && Attribute.IsDefined(p, typeof(PersistAttribute)));

        foreach (var property in persistedProperties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }
}