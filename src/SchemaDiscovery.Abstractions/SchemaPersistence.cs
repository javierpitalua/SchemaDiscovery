using System.Reflection;
using System.Text.Json;
using SchemaDiscovery.Abstractions.Models;

namespace SchemaDiscovery.Abstractions;

/// <summary>
/// Restores the values of <see cref="PersistAttribute"/>-marked properties
/// from a previously exported JSON file onto a freshly scanned object, so
/// hand-edited metadata survives repeated schema scans.
///
/// This is intentionally generic: it works for any object via reflection, so
/// new [Persist] properties (on tables, columns, or any future object type)
/// are picked up automatically with no changes needed here.
/// </summary>
public static class SchemaPersistence
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Merges [Persist] property values found in <paramref name="existingJson"/>
    /// onto <paramref name="target"/>. When <paramref name="target"/> is a
    /// <see cref="TableSchema"/>, its columns are additionally matched to their
    /// previous counterpart by column name, so per-column custom values (e.g.
    /// a persisted PropertyName) are preserved too.
    /// </summary>
    public static void ApplyPersistedValues(object target, JsonElement existingJson)
    {
        if (existingJson.ValueKind != JsonValueKind.Object)
            return;

        MergeProperties(target, existingJson);

        if (target is TableSchema table &&
            existingJson.TryGetProperty(nameof(TableSchema.Columns), out var existingColumns) &&
            existingColumns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in table.Columns)
            {
                var previousColumn = FindByName(existingColumns, column.Name);
                if (previousColumn is { ValueKind: JsonValueKind.Object } found)
                {
                    MergeProperties(column, found);
                }
            }
        }
    }

    private static JsonElement? FindByName(JsonElement array, string name)
    {
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(nameof(ColumnDefinition.Name), out var nameElement) &&
                string.Equals(nameElement.GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }

    private static void MergeProperties(object target, JsonElement existingJson)
    {
        var persistedProperties = target.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<PersistAttribute>() is not null);

        foreach (var property in persistedProperties)
        {
            if (!existingJson.TryGetProperty(property.Name, out var valueElement) ||
                valueElement.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            try
            {
                var value = valueElement.ValueKind == JsonValueKind.Null
                    ? null
                    : JsonSerializer.Deserialize(valueElement.GetRawText(), property.PropertyType, ReadOptions);

                property.SetValue(target, value);
            }
            catch (JsonException)
            {
                // Hand-edited value doesn't match the property's type; keep the freshly scanned default.
            }
        }
    }
}
