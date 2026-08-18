# schema-discovery

A .NET CLI tool that connects to a database, scans its schema (tables, views,
stored procedures, functions), and writes each object out as its own JSON
file — one file per object, e.g. `Customers.json`, `dbo.OrderLines.json`.

SQL Server is fully implemented. PostgreSQL and MySQL are scaffolded as
extension points (see below) so support can be added without touching the
CLI or the SQL Server code.

## Project layout

```
SchemaDiscovery.sln
src/
  SchemaDiscovery.Abstractions/          Shared models + provider interfaces (no DB dependency)
  SchemaDiscovery.Providers.SqlServer/   Full SQL Server implementation (Microsoft.Data.SqlClient)
  SchemaDiscovery.Providers.PostgreSql/  Stub — throws NotImplementedException, ready to fill in
  SchemaDiscovery.Providers.MySql/       Stub — throws NotImplementedException, ready to fill in
  SchemaDiscovery.Cli/                   Argument parsing, logging setup, provider registry, JSON export
```

The design is a simple strategy/factory pattern:

- `IDatabaseSchemaProvider` (in `SchemaDiscovery.Abstractions`) is the contract
  every database engine implements: `GetTablesAsync`, `GetViewsAsync`,
  `GetStoredProceduresAsync`, `GetFunctionsAsync`.
- `IDatabaseSchemaProviderFactory` creates a provider instance given a
  connection string and an `ILoggerFactory`.
- `SchemaDiscovery.Cli.ProviderFactory` is a small registry that maps a
  `--provider` name (`sqlserver`, `postgres`, `mysql`) to the right factory.
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

Bump `<Version>` in `SchemaDiscovery.Cli.csproj` before each release; the
`PackageId` (`SchemaDiscovery.Tool`), `ToolCommandName` (`schema-discovery`),
and package metadata (author, description, tags) also live there.

### Options

| Flag | Description |
|---|---|
| `-c`, `--connection-string` | Database connection string (required) |
| `-p`, `--provider` | `sqlserver` (default), `postgres`, or `mysql` |
| `-o`, `--output` | Output folder for JSON files (default `./schema-output`) |
| `-v`, `--verbose` | Enable debug-level console logging |
| `--skip-views` | Don't export views |
| `--skip-procedures` | Don't export stored procedures |
| `--skip-functions` | Don't export functions |
| `-h`, `--help` | Show usage |

### Output

Every table, view, stored procedure, and function is written as its own JSON
file in the output folder. Objects in the default schema (`dbo` for SQL
Server, `public` for Postgres) are named `<ObjectName>.json`; objects in any
other schema are named `<Schema>.<ObjectName>.json` to avoid collisions.

Example `dbo.Customers.json` (trimmed):

```json
{
  "schema": "dbo",
  "name": "Customers",
  "objectType": "Table",
  "databaseProvider": "sqlserver",
  "scannedAtUtc": "2026-08-18T12:00:00Z",
  "columns": [
    { "ordinalPosition": 1, "name": "Id", "dataType": "int", "isNullable": false, "isIdentity": true, "isPrimaryKey": true },
    { "ordinalPosition": 2, "name": "Email", "dataType": "nvarchar", "maxLength": 255, "isNullable": false, "isPrimaryKey": false }
  ],
  "primaryKeyColumns": ["Id"],
  "foreignKeys": [],
  "indexes": [
    { "name": "IX_Customers_Email", "isUnique": true, "isPrimaryKey": false, "indexType": "NONCLUSTERED", "columns": ["Email"] }
  ],
  "rowCountEstimate": 4213,
  "className": "Customer",
  "pluralClassName": "Customers"
}
```

`className`/`pluralClassName` (and a column's `propertyName`/`description`)
are omitted entirely until you set them by hand — nulls aren't written to
the file — and once set, they survive future scans. See "Custom properties
that survive a schema refresh" below.

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
`SchemaDiscovery.Abstractions` — copies the current value of every
`[Persist]` property from that file onto the freshly scanned object. Columns
are matched between the old and new file by `Name`, so per-column persisted
values (like `PropertyName`) are preserved too, not just table-level ones.
Only properties tagged `[Persist]` are carried forward; everything else is
always the fresh, scanned value.

Typical workflow:

1. Run `schema-discovery` — `dbo.Customers.json` is created with `ClassName: null`.
2. Hand-edit the file, setting `"className": "Customer"`.
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
   registered in `ProviderFactory` and selectable via `-p postgres` / `-p mysql`.

To add a brand-new engine entirely (e.g. Oracle, SQLite):

1. Create a new class library project under `src/`, referencing
   `SchemaDiscovery.Abstractions`.
2. Implement `IDatabaseSchemaProvider` and `IDatabaseSchemaProviderFactory`.
3. Add a `<ProjectReference>` to it from `SchemaDiscovery.Cli.csproj`.
4. Add `new YourProviderFactory()` to the array in `ProviderFactory.cs`.

## Logging

Logging uses `Microsoft.Extensions.Logging` with a console provider.
`-v`/`--verbose` switches the minimum level from `Information` to `Debug`,
which additionally logs each object as it's scanned and each file as it's
written. Errors (connection failures, unsupported providers, etc.) are
logged and the process exits with code `1`.
