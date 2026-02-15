# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Native Azure Key Vault integration — set `AzureKeyVaultUri` to load secrets as configuration values via `DefaultAzureCredential`
- AWS Secrets Manager and HashiCorp Vault wrapper script examples in SECURITY.md

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

[Unreleased]: https://github.com/mbentham/SqlAugur/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/mbentham/SqlAugur/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/mbentham/SqlAugur/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/mbentham/SqlAugur/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/mbentham/SqlAugur/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/mbentham/SqlAugur/releases/tag/v1.0.0
