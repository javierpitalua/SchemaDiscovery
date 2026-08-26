# SchemaDiscovery.Client

Reads the JSON files produced by the [schema-discovery](https://github.com/javierpitalua/SchemaDiscovery)
CLI back into typed .NET objects, so a code generator, ORM scaffolder, or
other tool can consume a scanned database schema without re-parsing JSON
itself.  This dll is intended to be consumed from Visual Studio T4 Templates.

Targets .NET Framework 4.7.2.

## Install

```bash
dotnet add package SchemaDiscovery.Client
```

## Usage

```csharp
using SchemaDiscovery.Client;

// inputDirectoryPath is the -o/--output folder schema-discovery wrote to,
// e.g. the ./schema-output produced by:
//   schema-discovery -c "..." -o ./schema-output
var project = ProjectLoader.LoadProject(@".\schema-output");

foreach (var table in project.Tables)
{
    Console.WriteLine($"{table.Schema}.{table.Name} ({table.Columns.Count} columns)");
}

foreach (var view in project.Views) { /* ... */ }
foreach (var routine in project.Routines) { /* stored procedures and functions */ }
```

`ProjectLoader` reads every `*.json` file from the `tables/`, `views/`,
`stored-procedures/` and `functions/` subfolders of the given directory and
sorts each one into `Project.Tables`, `Project.Views`, or `Project.Routines`
based on its `ObjectType`, using its own copy of the same model shapes
schema-discovery scanned them with (`SchemaDiscovery.Client.Models`) — so
hand-edited, `[Persist]`-ed fields like `TableSchema.ClassName` come through
untouched.

## Instance vs. static

```csharp
// Static convenience method — one call, no state to manage.
var project = ProjectLoader.LoadProject(@".\schema-output");

// Equivalent, via an instance — useful if you want to reuse/mock the loader.
var loader = new ProjectLoader();
var project = loader.Load(@".\schema-output");
```

See the [project README](https://github.com/javierpitalua/SchemaDiscovery#readme)
for details on the JSON file format and the `[Persist]` mechanism.
