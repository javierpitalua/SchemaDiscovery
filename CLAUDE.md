# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

This is an early-stage, mostly-scaffolded project. Most methods throw `NotImplementedException`, several entity classes are empty bodies, and `SchemaDiscovery.Infrastructure.DefaultModule` has its Autofac registration commented out. Expect to be implementing core functionality, not just modifying working code — check whether a method is a stub before assuming existing behavior.

## Commands

Solution file is `src/SchemaDiscoveryV1.slnx` (the new XML-based slnx format, not `.sln`).

```
dotnet build src/SchemaDiscoveryV1.slnx    # build all three projects
dotnet run --project src/SchemaDiscovery   # run the console entry point
```

There are no test projects yet. Targets `net10.0` (see `src/global.json`, SDK `10.0.0` with `rollForward: latestMajor`, prereleases allowed).

## Architecture

Three projects, layered as Core → SqlServer → SchemaDiscovery (console host), wired via Autofac:

- **SchemaDiscovery.Core** — provider-agnostic contracts and entities. No dependency on any specific database engine.
  - `Abstractions/IDatabaseCrawler` (namespace `SchemaDiscovery.Abstractions`) composes three narrower interfaces — `ITableExtractor`, `IViewExtractor`, `IStoredProcedureExtractor` — each returning one entity type. A provider crawler implements the union; extraction logic per object type is meant to live in separate per-type extractor classes rather than one large crawler.
  - `Engine/IExtractor` (namespace `SchemaDiscovery.Core.Engine`) is the top-level use case: given `ExtractionOptions` (connection string, `DatabaseType`, output path), it performs discovery and writes results to `OutputPath`. This is the entry point invoked by `Program.cs`; its real implementation should pick the right `IDatabaseCrawler` per `DatabaseType` and drive the extract-then-serialize workflow.
  - `Entities/DatabaseObject` is the abstract base (`Schema`, `Name`) for `Table`, `View`, `StoredProcedure`. `ColumnInfo` exists but is not yet wired to `Table`.
  - Note the namespace split: `SchemaDiscovery.Core.*` for abstractions/engine defined in this project, but `SchemaDiscovery.Abstractions` / `SchemaDiscovery.Entities` (no `.Core`) for the extractor interfaces and entities — this is existing, intentional-looking inconsistency, not a typo to silently "fix" mid-task.

- **SchemaDiscovery.SqlServer** — SQL Server-specific implementation. `SqlServerCrawler` implements `IDatabaseCrawler` by explicit interface implementation, delegating each of the three methods to an injected per-type extractor (`TableExtractor`, `ViewExtractor`, `StoredProcedureExtractor` in `Implementations/`). Each extractor's job is to query SQL Server system catalogs/INFORMATION_SCHEMA for its object type and map results into Core entities. Uses Serilog (`ILogger`) for logging.

- **SchemaDiscovery** — console composition root. `Program.cs` builds an Autofac container from `Infrastructure.DefaultModule`, resolves `IExtractor`, and calls `ExtractAsync` with a hardcoded `ExtractionOptions` (connection string, `DatabaseType.SqlServer`, local `output` path). When adding new provider implementations or extractor types, register them in `DefaultModule.Load`.

## Working in this codebase

- New database providers (MySql, PostgreSql, Oracle — already enumerated in `DatabaseType` but unimplemented) should follow the SqlServer project's shape: a project named `SchemaDiscovery.<Provider>`, a crawler implementing `IDatabaseCrawler`, and per-object-type extractor classes in an `Implementations/` folder.
- `ExtractionOptions.DatabaseType` is a plain string compared against the `DatabaseType` enum's `.ToString()`, not the enum itself — keep that in mind when wiring provider selection into `Extractor`.
