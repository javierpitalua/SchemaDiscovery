# SchemaDiscovery.Abstractions

Shared models and provider contracts for [schema-discovery](https://github.com/javierpitalua/SchemaDiscovery) —
a tool that scans a database's schema (tables, views, stored procedures,
functions) and exports each object as JSON.

This package has no database dependency. It's consumed by:

- Database provider implementations (SQL Server, PostgreSQL, MySQL) that
  implement `IDatabaseSchemaProvider` / `IDatabaseSchemaProviderFactory`.
- [`SchemaDiscovery.Client`](https://www.nuget.org/packages/SchemaDiscovery.Client),
  which reads the JSON files these providers produce back into typed objects.

## Contents

- `IDatabaseSchemaProvider` / `IDatabaseSchemaProviderFactory` — the contract
  every database engine implements to scan its own schema.
- `TableSchema`, `ViewSchema`, `RoutineSchema` and their supporting types
  (`ColumnDefinition`, `ForeignKeyDefinition`, `IndexDefinition`,
  `ParameterDefinition`) — the shape of a scanned schema object.
- `PersistAttribute` / `SchemaPersistence` — marks fields (like a hand-typed
  class name) that should survive being overwritten on the next scan, and
  carries them forward by reflection.

Targets `netstandard2.0`, so it can be referenced from both modern .NET and
.NET Framework (4.6.1+) projects.

See the [project README](https://github.com/javierpitalua/SchemaDiscovery#readme)
for the full picture, including how `[Persist]` works.
