using SchemaDiscovery.Client.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaDiscovery.Models;

namespace SchemaDiscovery.Client
{
    public class ProjectLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Must match the subfolder names SchemaExportService.GetSubfolderName writes to.
        private static readonly string[] SchemaObjectSubfolders = { "tables", "views", "stored-procedures", "functions" };

        /// <summary>
        /// Reads every "*.json" schema file previously exported by schema-discovery
        /// from the <c>tables</c>, <c>views</c>, <c>stored-procedures</c> and
        /// <c>functions</c> subfolders of <paramref name="inputDirectoryPath"/> and
        /// assembles them into a <see cref="Project"/>, sorted into tables, views
        /// and routines based on each file's <see cref="SchemaObjectBase.ObjectType"/>.
        /// </summary>
        public Project Load(string inputDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(inputDirectoryPath))
                throw new ArgumentException("Input directory path must be provided.", nameof(inputDirectoryPath));

            if (!Directory.Exists(inputDirectoryPath))
                throw new DirectoryNotFoundException($"Schema output directory not found: '{inputDirectoryPath}'.");

            var project = new Project();

            foreach (var subfolder in SchemaObjectSubfolders)
            {
                var subfolderPath = Path.Combine(inputDirectoryPath, subfolder);
                if (!Directory.Exists(subfolderPath))
                    continue;

                foreach (var filePath in Directory.EnumerateFiles(subfolderPath, "*.json", SearchOption.TopDirectoryOnly))
                {
                    LoadFile(filePath, project);
                }
            }

            return project;
        }

        /// <summary>Convenience wrapper around <see cref="Load"/> that doesn't require an instance.</summary>
        public static Project LoadProject(string inputDirectoryPath)
        {
            var loader = new ProjectLoader();
            return loader.Load(inputDirectoryPath);
        }

        private static void LoadFile(string filePath, Project project)
        {
            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                throw new IOException($"Could not read schema file '{filePath}'.", ex);
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty(nameof(SchemaObjectBase.ObjectType), out var objectTypeElement) ||
                objectTypeElement.ValueKind != JsonValueKind.String ||
                !Enum.TryParse(objectTypeElement.GetString(), ignoreCase: true, out SchemaObjectType objectType))
            {
                throw new InvalidDataException(
                    $"File '{filePath}' does not contain a recognized '{nameof(SchemaObjectBase.ObjectType)}' value.");
            }

            switch (objectType)
            {
                case SchemaObjectType.Table:
                    project.Tables.Add(Deserialize<TableSchema>(root, filePath));
                    break;

                case SchemaObjectType.View:
                    project.Views.Add(Deserialize<ViewSchema>(root, filePath));
                    break;

                case SchemaObjectType.StoredProcedure:
                case SchemaObjectType.Function:
                    project.Routines.Add(DeserializeRoutine(root, objectType, filePath));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported schema object type '{objectType}' in file '{filePath}'.");
            }
        }

        private static T Deserialize<T>(JsonElement root, string filePath)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonOptions)
                    ?? throw new InvalidDataException($"File '{filePath}' deserialized to a null {typeof(T).Name}.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Could not deserialize '{filePath}' as {typeof(T).Name}.", ex);
            }
        }

        // RoutineSchema has no parameterless constructor (its ObjectType is fixed
        // at construction to StoredProcedure or Function), so it can't go through
        // plain JsonSerializer.Deserialize<RoutineSchema>. Its properties are
        // mapped by hand instead.
        private static RoutineSchema DeserializeRoutine(JsonElement root, SchemaObjectType objectType, string filePath)
        {
            try
            {
                var routine = new RoutineSchema(objectType)
                {
                    Schema = GetString(root, nameof(SchemaObjectBase.Schema)) ?? string.Empty,
                    Name = GetString(root, nameof(SchemaObjectBase.Name)) ?? string.Empty,
                    DatabaseProvider = GetString(root, nameof(SchemaObjectBase.DatabaseProvider)) ?? string.Empty,
                    ReturnType = GetString(root, nameof(RoutineSchema.ReturnType)),
                    Definition = GetString(root, nameof(RoutineSchema.Definition))
                };

                if (root.TryGetProperty(nameof(SchemaObjectBase.ScannedAtUtc), out var scannedAtElement) &&
                    scannedAtElement.ValueKind != JsonValueKind.Null)
                {
                    routine.ScannedAtUtc = scannedAtElement.GetDateTimeOffset();
                }

                if (root.TryGetProperty(nameof(RoutineSchema.Parameters), out var parametersElement) &&
                    parametersElement.ValueKind == JsonValueKind.Array)
                {
                    routine.Parameters = JsonSerializer.Deserialize<List<ParameterDefinition>>(
                        parametersElement.GetRawText(), JsonOptions) ?? new List<ParameterDefinition>();
                }

                return routine;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Could not deserialize '{filePath}' as {nameof(RoutineSchema)}.", ex);
            }
        }

        private static string GetString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
