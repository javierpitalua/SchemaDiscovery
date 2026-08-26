# schema-discovery

A .NET CLI tool that connects to a database, scans its schema (tables, views,
stored procedures, functions), and writes each object out as its own JSON
file — one file per object, e.g. `Customers.json`, `dbo.OrderLines.json`.

SQL Server is fully implemented. PostgreSQL and MySQL are scaffolded as
extension points (see below) so support can be added without touching the
CLI or the SQL Server code.

## Project layout

```
SchemaDiscoveryV1.sln
src/
  SchemaDiscovery.Cli/                   01 - CLI: argument parsing, Autofac wiring, JSON export
  SchemaDiscovery/                       02 - Core: schema models, provider interfaces, Humanizer, persistence
  SchemaDiscovery.Models/                Legacy models project, kept temporarily but no longer referenced
                                          by anything — its classes now live in SchemaDiscovery (see below)
  SchemaDiscovery.Providers.SqlServer/   03 - Full SQL Server implementation (Microsoft.Data.SqlClient)
  SchemaDiscovery.Providers.PostgreSql/  03 - Stub — throws NotImplementedException, ready to fill in
  SchemaDiscovery.Providers.MySql/       03 - Stub — throws NotImplementedException, ready to fill in
  SchemaDiscovery.Client/                04 - .NET Framework 4.7.2 library that reads the exported JSON back
  SchemaDiscovery.Client.Tests/          05 - Tests for SchemaDiscovery.Client
  SchemaDiscovery.Tests/                 05 - Tests for SchemaDiscovery (NUnit)
```

> **Note:** `SchemaDiscovery.Models` is mid-deprecation — its classes
> (`TableSchema`, `ColumnDefinition`, etc., namespace `SchemaDiscovery.Models`)
> were moved into the `SchemaDiscovery` project. The project is kept in the
> solution for now but nothing references it anymore; it's expected to be
> deleted in a follow-up.

The design is a simple strategy/factory pattern:

- `IDatabaseSchemaProvider` (in `SchemaDiscovery`) is the contract every
  database engine implements: `GetTablesAsync`, `GetViewsAsync`,
  `GetStoredProceduresAsync`, `GetFunctionsAsync`.
- `IDatabaseSchemaProviderFactory` creates a provider instance given a
  connection string, an `ILoggerFactory`, and the output `CultureLanguages`.
- `SchemaDiscovery.Cli.ProviderFactory` is a small registry that maps a
  `--provider` name (`sqlserver`, `postgres`, `mysql`) to the right factory;
  factories are registered with Autofac in
  `SchemaDiscovery.Cli/DependencyResolution/DefaultModule.cs`.
- `SchemaExportService` is provider-agnostic: it just calls the interface and
  serializes whatever comes back, so it never needs to change when a new
  provider is added.

## Building

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build
```

## Running

```bash
dotnet run --project src/SchemaDiscovery.Cli -- \
  -c "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  -o ./schema-output
```

Or, after `dotnet publish`, run the produced `schema-discovery` executable directly:

```bash
schema-discovery -c "Server=.;Database=MyDb;User Id=sa;Password=***;" -p sqlserver -v
```

## Installing as a .NET tool

`SchemaDiscovery.Cli` is set up to be packed and installed as a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools),
so it can be run as `schema-discovery` from anywhere without `dotnet run`.

Pack it:

```bash
dotnet pack src/SchemaDiscovery.Cli -c Release
```

This produces `src/SchemaDiscovery.Cli/nupkg/SchemaDiscovery.Tool.1.0.0.nupkg`.

Install it globally from that local package folder:

```bash
dotnet tool install --global SchemaDiscovery.Tool --add-source ./src/SchemaDiscovery.Cli/nupkg
```

Or install it locally into a repo (creates/uses a `.config/dotnet-tools.json` manifest):

```bash
dotnet new tool-manifest   # only if one doesn't already exist
dotnet tool install SchemaDiscovery.Tool --add-source ./src/SchemaDiscovery.Cli/nupkg
```

Once installed, run it directly:

```bash
schema-discovery -c "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;" -o ./schema-output
```

To publish it for others to install without a local package source, push the
`.nupkg` to nuget.org or a private feed, then:

```bash
dotnet tool install --global SchemaDiscovery.Tool
```

To upgrade or remove:

```bash
dotnet tool update --global SchemaDiscovery.Tool
dotnet tool uninstall --global SchemaDiscovery.Tool
```

## SchemaDiscovery.Client (reading the JSON output from code)

`SchemaDiscovery.Client` (in `SchemaDiscovery.Client/`, .NET Framework 4.7.2)
reads the JSON files `schema-discovery` writes back into typed `TableSchema`
/ `ViewSchema` / `RoutineSchema` objects, for tools — code generators, ORM
scaffolders — that want to consume a scanned schema without parsing JSON
themselves:

```bash
dotnet add package SchemaDiscovery.Client
```

```csharp
var project = SchemaDiscovery.Client.ProjectLoader.LoadProject(@".\schema-output");
```

Being a classic .NET Framework 4.7.2 library, it doesn't take a package
dependency on the (net10.0-only) `SchemaDiscovery` project; instead it keeps
its own copy of the model types under `SchemaDiscovery.Client/Models/`. See
`SchemaDiscovery.Client/README.md` for the full API.

### Releasing SchemaDiscovery.Cli / SchemaDiscovery.Client

There is currently no CI workflow that publishes packages automatically —
both are packed and pushed by hand:

```bash
dotnet pack src/SchemaDiscovery.Cli -c Release      # -> nupkg/SchemaDiscovery.Tool.<version>.nupkg
dotnet pack src/SchemaDiscovery.Client -c Release    # -> nupkg/SchemaDiscovery.Client.<version>.nupkg

dotnet nuget push src/SchemaDiscovery.Cli/nupkg/SchemaDiscovery.Tool.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>
dotnet nuget push src/SchemaDiscovery.Client/nupkg/SchemaDiscovery.Client.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>
```

Bump `<Version>` in `SchemaDiscovery.Cli.csproj` (`PackageId`
`SchemaDiscovery.Tool`) and `SchemaDiscovery.Client.csproj` (`PackageId`
`SchemaDiscovery.Client`) before each release; both currently sit at `1.0.0`.

### Options

| Flag | Description |
|---|---|
| `-c`, `--connection-string` | Database connection string (required) |
| `-p`, `--provider` | `sqlserver` (default), `postgres`, or `mysql` |
| `-o`, `--output` | Output folder for JSON files (default `./schema-output`) |
| `-l`, `--language` | Output language for generated text: `en` (default) or `es` |
| `-v`, `--verbose` | Enable debug-level console logging |
| `--skip-views` | Don't export views |
| `--skip-procedures` | Don't export stored procedures |
| `--skip-functions` | Don't export functions |
| `-h`, `--help` | Show usage |

### Output

A `projectInfo.json` file is written at the root of the output folder with
scan metadata (`providerName`, `scannedAtUtc`, `cultureLanguage`). Every
table, view, stored procedure, and function is then written as its own JSON
file into a subfolder by kind — `tables/`, `views/`, `stored-procedures/`,
`functions/`. Objects in the default schema (`dbo` for SQL Server, `public`
for Postgres) are named `<ObjectName>.json`; objects in any other schema are
named `<Schema>.<ObjectName>.json` to avoid collisions.

Example `tables/Customers.json` (trimmed):

```json
{
  "schema": "dbo",
  "name": "Customers",
  "className": "Customer",
  "pluralClassName": "Customers",
  "qualifiedName": "dbo.Customers",
  "objectType": "Table",
  "columns": [
    { "ordinalPosition": 1, "name": "Id", "dataType": "int", "isNullable": false, "isIdentity": true, "isPrimaryKey": true },
    { "ordinalPosition": 2, "name": "Email", "dataType": "nvarchar", "maxLength": 255, "isNullable": false, "isPrimaryKey": false }
  ],
  "primaryKeyColumns": ["Id"],
  "foreignKeys": [],
  "indexes": [
    { "name": "IX_Customers_Email", "isUnique": true, "isPrimaryKey": false, "indexType": "NONCLUSTERED", "columns": ["Email"] }
  ],
  "rowCountEstimate": 4213
}
```

`className`/`pluralClassName`/`displayName`/`pluralDisplayName` (and a
column's `propertyName`/`description`) are omitted entirely until you set
them by hand — nulls aren't written to the file — and once set, they survive
future scans. See "Custom properties that survive a schema refresh" below.

## Custom properties that survive a schema refresh

Every scanned property (column type, indexes, row counts, ...) is
regenerated on every run — that's the point of a scanner. But some fields
are metadata a person wants to type in by hand and *keep*, e.g. the class
name to generate for a table. Those are marked with `[Persist]`:

```csharp
public class TableSchema : SchemaObjectBase
{
    [Persist]
    public string? ClassName { get; set; }

    [Persist]
    public string? PluralClassName { get; set; }
}
```

`ColumnDefinition` has the same idea with `PropertyName` and `Description`.

**How it works:** before `SchemaExportService` overwrites `dbo.Customers.json`,
it reads the *existing* file (if any) and — via `SchemaPersistence` in
`SchemaDiscovery` — copies the current value of every
`[Persist]` property from that file onto the freshly scanned object. Columns
are matched between the old and new file by `Name`, so per-column persisted
values (like `PropertyName`) are preserved too, not just table-level ones.
Only properties tagged `[Persist]` are carried forward; everything else is
always the fresh, scanned value.

Typical workflow:

1. Run `schema-discovery` — `tables/Customers.json` is created without a `className`.
2. Hand-edit the file, adding `"className": "Customer"`.
3. Run `schema-discovery` again (schema changed, or just re-running) —
   `"className": "Customer"` is still there; everything else reflects the
   current database schema.

If a table or column is renamed or dropped in the database, its old
persisted values have nothing to match onto and are lost — there's no way to
infer that `Customer` should map to a renamed table without another signal.

To persist additional custom values, add a property with `[Persist]` to
`TableSchema`, `ColumnDefinition`, or any other model (`ViewSchema`,
`RoutineSchema`) — no other code changes are needed, since
`SchemaPersistence` discovers `[Persist]` properties via reflection.

## Adding a new provider (e.g. finishing PostgreSQL or MySQL)

1. Open `SchemaDiscovery.Providers.PostgreSql` (or `.MySql`) — the project
   already exists and is wired into the CLI and solution.
2. Uncomment/add the relevant ADO.NET package in its `.csproj` (`Npgsql` or
   `MySqlConnector`).
3. Implement `GetTablesAsync`, `GetViewsAsync`, `GetStoredProceduresAsync`,
   and `GetFunctionsAsync` in the provider class, querying
   `information_schema` / the engine's catalog views. Use
   `SqlServerSchemaProvider` in `SchemaDiscovery.Providers.SqlServer` as a
   reference implementation — the shape (list objects, then fetch
   columns/keys/indexes per object) carries over directly.
4. No changes are needed in `SchemaDiscovery.Cli` — the provider is already
   registered in `DefaultModule` and selectable via `-p postgres` / `-p mysql`.

To add a brand-new engine entirely (e.g. Oracle, SQLite):

1. Create a new class library project under `src/`, referencing
   `SchemaDiscovery`.
2. Implement `IDatabaseSchemaProvider` and `IDatabaseSchemaProviderFactory`.
3. Add a `<ProjectReference>` to it from `SchemaDiscovery.Cli.csproj`.
4. Register it (`builder.RegisterType<YourProviderFactory>().As<IDatabaseSchemaProviderFactory>()...`)
   in `SchemaDiscovery.Cli/DependencyResolution/DefaultModule.cs`.

## Logging

Logging uses `Microsoft.Extensions.Logging` with a console provider.
`-v`/`--verbose` switches the minimum level from `Information` to `Debug`,
which additionally logs each object as it's scanned and each file as it's
written. Errors (connection failures, unsupported providers, etc.) are
logged and the process exits with code `1`.
