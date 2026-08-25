using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Cli;

public sealed class SchemaExportService
{
    /// <summary>
    /// Property order for exported TableSchema JSON, overriding the default
    /// base-class-then-derived-class order so persisted/computed fields read
    /// naturally next to the identity fields they relate to.
    /// </summary>
    private static readonly string[] TableSchemaPropertyOrder =
    [
        nameof(TableSchema.Schema),
        nameof(TableSchema.Name),
        nameof(TableSchema.ClassName),
        nameof(TableSchema.PluralClassName),
        nameof(TableSchema.DisplayName),
        nameof(TableSchema.PluralDisplayName),
        nameof(TableSchema.QualifiedName),
        nameof(TableSchema.ObjectType),
        nameof(TableSchema.Columns),
        nameof(TableSchema.PrimaryKeyColumns),
        nameof(TableSchema.ForeignKeys),
        nameof(TableSchema.Indexes),
        nameof(TableSchema.RowCountEstimate),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(ApplyTableSchemaPropertyOrder)
    };

    private static void ApplyTableSchemaPropertyOrder(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(TableSchema))
            return;

        for (var i = 0; i < typeInfo.Properties.Count; i++)
        {
            var propertyIndex = Array.IndexOf(TableSchemaPropertyOrder, typeInfo.Properties[i].Name);
            typeInfo.Properties[i].Order = propertyIndex >= 0 ? propertyIndex : TableSchemaPropertyOrder.Length;
        }
    }

    private readonly ILogger<SchemaExportService> _logger;

    public SchemaExportService(ILogger<SchemaExportService> logger)
    {
        _logger = logger;
    }

    public async Task<int> ExportAsync(
        IDatabaseSchemaProvider provider,
        string outputDirectory,
        bool skipViews,
        bool skipProcedures,
        bool skipFunctions,
        string language,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var exportedCount = 0;

        var lang = language.ToLowerInvariant() switch
        {
            "en" => CultureLanguages.English,
            "es" => CultureLanguages.Spanish,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Language must be 'en' or 'es'.")
        };  

       var projectInfo = new ProjectInfo
       {
           ProviderName = provider.ProviderName,
           ScannedAtUtc = DateTimeOffset.UtcNow,
           CultureLanguage = lang.ToString()
       };

        var projectInfoFileName = Path.Combine(outputDirectory, "projectInfo.json");
        await WriteObjectAsync(projectInfoFileName, projectInfo, cancellationToken);

        var tables = await provider.GetTablesAsync(cancellationToken);
        foreach (var table in tables)
        {
            await WriteObjectAsync(outputDirectory, "table", table, cancellationToken);
            exportedCount++;
        }
        _logger.LogInformation("Exported {Count} table(s).", tables.Count);

        if (!skipViews)
        {
            var views = await provider.GetViewsAsync(cancellationToken);
            foreach (var view in views)
            {
                await WriteObjectAsync(outputDirectory, "view", view, cancellationToken);
                exportedCount++;
            }
            _logger.LogInformation("Exported {Count} view(s).", views.Count);
        }

        if (!skipProcedures)
        {
            var procedures = await provider.GetStoredProceduresAsync(cancellationToken);
            foreach (var procedure in procedures)
            {
                await WriteObjectAsync(outputDirectory, "procedure", procedure, cancellationToken);
                exportedCount++;
            }
            _logger.LogInformation("Exported {Count} stored procedure(s).", procedures.Count);
        }

        if (!skipFunctions)
        {
            var functions = await provider.GetFunctionsAsync(cancellationToken);
            foreach (var function in functions)
            {
                await WriteObjectAsync(outputDirectory, "function", function, cancellationToken);
                exportedCount++;
            }
            _logger.LogInformation("Exported {Count} function(s).", functions.Count);
        }

        return exportedCount;
    }

    private async Task WriteObjectAsync(
        string outputDirectory, string kind, SchemaObjectBase schemaObject, CancellationToken cancellationToken)
    {
        var kindDirectory = Path.Combine(outputDirectory, GetSubfolderName(kind));
        Directory.CreateDirectory(kindDirectory);

        var fileName = BuildFileName(schemaObject);
        var filePath = Path.Combine(kindDirectory, fileName);

        await TryRestorePersistedValuesAsync(schemaObject, filePath, cancellationToken);

        _logger.LogDebug("Writing {Kind} '{Qualified}' to {File}", kind, schemaObject.QualifiedName, filePath);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, schemaObject, schemaObject.GetType(), JsonOptions, cancellationToken);
    }

    private async Task WriteObjectAsync(
       string outputFileName, object objectInfo, CancellationToken cancellationToken)
    {   
        await using var stream = File.Create(outputFileName);
        await JsonSerializer.SerializeAsync(stream, objectInfo, objectInfo.GetType(), JsonOptions, cancellationToken);
    }

    /// <summary>
    /// If a JSON file already exists for this object (from a previous scan),
    /// reads it and carries every [Persist]-marked property value (e.g.
    /// TableSchema.ClassName, ColumnDefinition.PropertyName) forward onto the
    /// freshly scanned object, so hand-edited metadata isn't lost when the
    /// file is overwritten below.
    /// </summary>
    private async Task TryRestorePersistedValuesAsync(
        SchemaObjectBase schemaObject, string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            await using var existingStream = File.OpenRead(filePath);
            using var existingDocument = await JsonDocument.ParseAsync(existingStream, cancellationToken: cancellationToken);

            SchemaPersistence.ApplyPersistedValues(schemaObject, existingDocument.RootElement);

            _logger.LogDebug("Restored persisted custom values from existing file {File}.", filePath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex,
                "Could not read existing file {File} to preserve custom values; it will be overwritten with blank custom values.",
                filePath);
        }
    }

    /// <summary>
    /// Produces "&lt;TableName&gt;.json" for objects in the default schema (e.g. "dbo"),
    /// and "&lt;Schema&gt;.&lt;TableName&gt;.json" otherwise, to avoid collisions between
    /// same-named objects in different schemas.
    /// </summary>
    private static string BuildFileName(SchemaObjectBase schemaObject)
    {
        var safeSchema = Sanitize(schemaObject.Schema);
        var safeName = Sanitize(schemaObject.Name);

        var isDefaultSchema = string.IsNullOrEmpty(safeSchema)
            || safeSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase)
            || safeSchema.Equals("public", StringComparison.OrdinalIgnoreCase);

        return isDefaultSchema ? $"{safeName}.json" : $"{safeSchema}.{safeName}.json";
    }

    private static string GetSubfolderName(string kind) => kind switch
    {
        "table" => "tables",
        "view" => "views",
        "procedure" => "stored-procedures",
        "function" => "functions",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown schema object kind.")
    };

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}
