using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SchemaDiscovery.Abstractions;
using SchemaDiscovery.Abstractions.Models;

namespace SchemaDiscovery.Cli;

public sealed class SchemaExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

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
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var exportedCount = 0;

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
        var fileName = BuildFileName(schemaObject);
        var filePath = Path.Combine(outputDirectory, fileName);

        await TryRestorePersistedValuesAsync(schemaObject, filePath, cancellationToken);

        _logger.LogDebug("Writing {Kind} '{Qualified}' to {File}", kind, schemaObject.QualifiedName, filePath);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, schemaObject, schemaObject.GetType(), JsonOptions, cancellationToken);
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

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}
