# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- `sp_blitz_plan_compare` tool — runs Brent Ozar's `sp_BlitzPlanCompare` across two SQL Server instances without using linked servers. Captures a plan snapshot on one server and passes it to the compare call on a second server via a strongly-typed `@CompareToXML` parameter. Requires the First Responder Kit `demon_hunters` branch installed on both servers until it merges to main.

### Changed
- Upgraded T-SQL parser from `TSql170Parser` to `TSql180Parser` (ScriptDom 170.157.0 → 180.6.0), enabling parse coverage of SQL Server 2022/2025-generation syntax
- Upgraded `Microsoft.Data.SqlClient` 6.1.4 → 7.0.0
- Upgraded `ModelContextProtocol` 0.8.0-preview.1 → 1.2.0 (first stable GA release), closing the preview-SDK caveat previously called out in README Known Risks
- Upgraded `coverlet.collector` 8.0.0 → 10.0.0 and `Microsoft.NET.Test.Sdk` 18.0.1 → 18.4.0 (test tooling)
- Bumped `Azure.Extensions.AspNetCore.Configuration.Secrets` 1.4.0 → 1.5.0, `Microsoft.Extensions.Hosting` 10.0.3 → 10.0.7, `System.Threading.RateLimiting` 10.0.3 → 10.0.7, `Testcontainers` 4.10.0 → 4.11.0

### Security
- Block `OPENROWSET BULK` via new `BulkOpenRowset` AST visitor override (previously `OPENROWSET(BULK …, SINGLE_CLOB)` file-read syntax was not explicitly blocked at the AST level)
- Block `OPENROWSET` against external providers (Cosmos DB etc., `OpenRowsetCosmos`) and internal `OPENROWSET` variants (`InternalOpenRowset`) — new AST types exposed by the ScriptDom 180 upgrade

## [1.4.0] - 2026-03-01

### Added
- Response size optimisations for DBA diagnostic tools — per-tool column exclusion, column truncation, and smart parameter defaults reduce response sizes by 90–99% while preserving full data access via `includeQueryPlans`/`includeXmlReports`/`verbose` flags
- `maxRows` parameter for tools with variable-length output (sp_BlitzIndex, sp_BlitzLock, sp_HealthParser, sp_LogHunter, sp_IndexCleanup, sp_QueryReproBuilder)
- Glama directory listing with `glama.json`

### Fixed
- sp_HealthParser `query_plan` columns now excluded by default and respect the `includeQueryPlans` parameter
- Double-prefixed metric columns in sp_QuickieStore output (e.g. `min_min_spills`) caused by duplicate entries with baked-in prefixes
- Simplified `BuildBlitzOptions` to use a single return path matching the pattern of other format option builders

## [1.3.1] - 2026-02-18

### Added
- Native Azure Key Vault integration — set `AzureKeyVaultUri` to load secrets as configuration values via `DefaultAzureCredential`
- AWS Secrets Manager and HashiCorp Vault wrapper script examples in SECURITY.md
- MCP Registry `server.json` for official registry publishing

### Fixed
- Dockerfile `dotnet publish` missing restore step
- Added `:Z` flag to volume-mount examples for SELinux compatibility (Fedora, RHEL)

## [1.3.0] - 2026-02-14

### Added
- NuGet global tool packaging (`dotnet tool install -g SqlAugur`)
- Docker and Podman container support with Dockerfile
- Configuration search path (app directory, user config directory, current working directory, env vars, CLI args)
- Server version reported from assembly metadata
- Schema exploration toolset: `list_programmable_objects`, `get_object_definition`, `get_extended_properties`, `get_object_dependencies`
- Mermaid ER diagram generation (`get_mermaid_diagram`)
- Toolset reorganization: Schema Exploration and Diagrams as always-available toolsets in dynamic mode
- Documentation rework: restructured README, added CONTRIBUTING.md and CHANGELOG.md

### Changed
- **Breaking:** Renamed project from SqlServerMcp to SqlAugur (config section `"SqlAugur"`, CLI command `sqlaugur`)
- Removed create/modify dates from `list_programmable_objects` output

### Fixed
- Sanitized markdown table cells in TableDescribeService to prevent output corruption

## [1.2.0] - 2026-02-13

### Changed
- **Breaking:** Stripped JSON overhead from all tool responses to reduce token usage — `list_servers` and `list_databases` return plain text, `read_data` returns only `truncated` and `rows`, stored procedure tools return a JSON array of `{truncated, rows}` objects
- **Breaking:** `get_query_plan` writes XML to a `.sqlplan` file instead of returning inline JSON (new required `outputPath` parameter)
- Tightened tool descriptions for diagram and schema overview tools
- Restricted diagram output to `.puml` and `.mmd` file extensions
- Used `Path.GetExtension` for file extension validation
- Extracted `ToolHelper.SaveToFileAsync` shared helper for file-write validation

## [1.1.0] - 2026-02-13

### Added
- Include/exclude table filters for diagram and schema overview tools
- Multi-schema support (comma-separated schema lists) for diagram and schema overview tools

### Fixed
- Wrapped `SqlException` in `ToolHelper` for consistent error reporting
- Deduplicated plan collection logic in query plan tool

## [1.0.1] - 2026-02-12

### Changed
- Updated all NuGet packages to latest versions
- Migrated to xUnit v3

## [1.0.0] - 2026-02-12

### Added
- Core tools: `list_servers`, `list_databases`, `read_data`, `get_query_plan`, `get_schema_overview`, `describe_table`
- AST-based query validation using `TSql170Parser` — blocks non-SELECT statements, SELECT INTO, OPENROWSET/OPENQUERY/OPENDATASOURCE, linked servers, MAXRECURSION
- PlantUML ER diagram generation with smart cardinality detection and compact mode
- First Responder Kit integration (sp_Blitz, sp_BlitzFirst, sp_BlitzCache, sp_BlitzIndex, sp_BlitzWho, sp_BlitzLock) with output parameter blocking
- DarlingData integration (sp_PressureDetector, sp_QuickieStore, sp_HealthParser, sp_LogHunter, sp_HumanEventsBlockViewer, sp_IndexCleanup, sp_QueryReproBuilder) with logging/output parameter blocking
- sp_WhoIsActive integration with destination_table/schema parameter blocking
- Progressive discovery mode with `list_toolsets`, `get_toolset_tools`, `enable_toolset` meta-tools
- Concurrency and throughput rate limiting
- Multi-server connection support
- Integration tests with Testcontainers and Podman (SQL Server 2025)
- Security guide (SECURITY.md) with credential management and SQL account hardening

[Unreleased]: https://github.com/mbentham/SqlAugur/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/mbentham/SqlAugur/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/mbentham/SqlAugur/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/mbentham/SqlAugur/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/mbentham/SqlAugur/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/mbentham/SqlAugur/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/mbentham/SqlAugur/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/mbentham/SqlAugur/releases/tag/v1.0.0
