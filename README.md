# SQL Server MCP — Safe, Read-Only Database Access for AI Assistants

A .NET MCP (Model Context Protocol) server that gives AI assistants safe, read-only access to SQL Server databases. Every query is validated at the AST level using Microsoft's official T-SQL parser — not regex — so keyword-in-string tricks and comment-based bypasses don't work. Supports multiple server connections and ships with integrated DBA diagnostic tooling from the First Responder Kit, DarlingData, and sp_WhoIsActive.

## Features

**Security**
- Read-only by design — only SELECT and CTE queries are permitted
- AST-based query validation using [ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom) (not regex)
- Parameter blocking on all diagnostic stored procedures to prevent writes
- Concurrency and throughput rate limiting

**Database tooling**
- Multi-server support — named connections to multiple SQL Server instances
- ER diagram generation — PlantUML diagrams with smart cardinality detection
- Table documentation — Markdown descriptions of columns, indexes, and constraints
- Query plan analysis — estimated or actual XML execution plans
- DBA diagnostics — optional integration with First Responder Kit, DarlingData, and sp_WhoIsActive
- Progressive discovery — optional dynamic toolset mode reduces initial context window usage by exposing DBA tools on demand

## Prerequisites

**Required:**
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Access to one or more SQL Server instances

**Optional (for DBA tools):**

Each toolkit is enabled independently — install only what you need:

- [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) (`EnableFirstResponderKit`) — for `sp_blitz`, `sp_blitz_first`, `sp_blitz_cache`, `sp_blitz_index`, `sp_blitz_who`, `sp_blitz_lock`
- [DarlingData toolkit](https://github.com/erikdarling/DarlingData) (`EnableDarlingData`) — for `sp_pressure_detector`, `sp_quickie_store`, `sp_health_parser`, `sp_log_hunter`, `sp_human_events_block_viewer`, `sp_index_cleanup`, `sp_query_repro_builder`
- [sp_WhoIsActive](http://whoisactive.com/) (`EnableWhoIsActive`) — for `sp_whoisactive`

## Configuration

Copy the example configuration and edit it with your SQL Server connections:

```bash
cp SqlServerMcp/appsettings.example.json SqlServerMcp/appsettings.json
```

> **Note:** Place `appsettings.json` in the same directory as the server DLL. For published builds, copy it into the publish output directory (see [MCP Client Setup](#mcp-client-setup)).

**Example configuration (Windows Authentication - Recommended):**

```json
{
  "SqlServerMcp": {
    "Servers": {
      "production": {
        "ConnectionString": "Server=myserver;Database=master;Integrated Security=True;TrustServerCertificate=False;Encrypt=True;"
      }
    },
    "MaxRows": 1000,
    "CommandTimeoutSeconds": 30,
    "MaxConcurrentQueries": 5,
    "MaxQueriesPerMinute": 60,
    "EnableFirstResponderKit": false,
    "EnableDarlingData": false,
    "EnableWhoIsActive": false,
    "EnableDynamicToolsets": false
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Servers` | — | Named SQL Server connections (name → connection string) |
| `MaxRows` | 1000 | Maximum rows returned per query |
| `CommandTimeoutSeconds` | 30 | SQL command timeout for all queries and procedures |
| `MaxConcurrentQueries` | 5 | Maximum number of SQL queries that can execute concurrently |
| `MaxQueriesPerMinute` | 60 | Maximum queries allowed per minute (token bucket rate limit) |
| `EnableFirstResponderKit` | false | Enable First Responder Kit diagnostic tools (sp_Blitz, sp_BlitzFirst, sp_BlitzCache, sp_BlitzIndex, sp_BlitzWho, sp_BlitzLock) |
| `EnableDarlingData` | false | Enable DarlingData diagnostic tools (sp_PressureDetector, sp_QuickieStore, sp_HealthParser, sp_LogHunter, sp_HumanEventsBlockViewer, sp_IndexCleanup, sp_QueryReproBuilder) |
| `EnableWhoIsActive` | false | Enable sp_WhoIsActive session monitoring |
| `EnableDynamicToolsets` | false | Enable progressive tool discovery. When true, DBA tools are not loaded at startup — instead, 3 discovery meta-tools (`list_toolsets`, `get_toolset_tools`, `enable_toolset`) let the AI enable toolsets on demand. Reduces initial context window usage. The individual `Enable*` flags still control which toolsets are allowed. |

> **Security Note:** `appsettings.json` is gitignored to prevent accidental credential commits. See [SECURITY.md](SECURITY.md) for recommended authentication methods including Windows Authentication, Azure Managed Identity, and secure credential storage options.

## Build & Run

```bash
# Build
dotnet build

# Run (development)
dotnet run --project SqlServerMcp

# Publish (recommended for MCP client use)
dotnet publish SqlServerMcp -c Release -o SqlServerMcp/publish

# Unit tests (no external dependencies)
dotnet test SqlServerMcp.Tests

# All tests including integration (requires Podman — see below)
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test
```

### Integration Tests

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to run a SQL Server 2025 container via Podman (rootless). They test the core services (query execution, diagram generation, schema overview, table describe) against a real database.

**Prerequisites:**
- [Podman](https://podman.io/) installed
- Podman user socket active: `systemctl --user start podman.socket`

**Running:**
```bash
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test SqlServerMcp.IntegrationTests
```

The first run pulls `mcr.microsoft.com/mssql/server:2025-latest` (~1.5 GB). Subsequent runs reuse the cached image and complete in ~15 seconds. Unit tests (`SqlServerMcp.Tests`) require no container runtime and always work with plain `dotnet test SqlServerMcp.Tests`.

## MCP Client Setup

**Recommended: published build**

MCP clients may launch the server from an arbitrary working directory, which can prevent `dotnet run` from finding `appsettings.json`. Publishing the server to a standalone directory avoids this:

```bash
dotnet publish SqlServerMcp -c Release -o SqlServerMcp/publish
cp SqlServerMcp/appsettings.json SqlServerMcp/publish/
```

Then add to your MCP client configuration (Claude Desktop, Claude Code, etc.):

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["/absolute/path/to/SqlServerMcp/publish/SqlServerMcp.dll"]
    }
  }
}
```

**Alternative: dotnet run (development only)**

This works when the MCP client launches from the project directory, but may fail if the client uses a different working directory:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/SqlServerMcp"]
    }
  }
}
```

## Tools

By default, the server exposes up to 21 tools: 7 core tools that are always available, and 14 optional DBA tools controlled by three independent flags. When `EnableDynamicToolsets` is true, the server starts with only the 7 core tools plus 3 discovery meta-tools — DBA toolsets are loaded on demand to reduce initial context window usage.

### Core Tools

| Tool | Description |
|------|-------------|
| `list_servers` | Lists available SQL Server instances configured in `appsettings.json`. Call this first to discover server names. |
| `list_databases` | Lists all databases on a named server with names, IDs, states, and creation dates. |
| `read_data` | Executes a read-only SQL SELECT query against a specific database. Only `SELECT` and `WITH` (CTE) queries are allowed. Results returned as JSON with a configurable row limit. |
| `get_schema_overview` | Returns a concise Markdown overview of the database schema: tables, columns with data types, primary keys, foreign key references, unique constraints, check constraints, and defaults. Supports a `compact` mode that shows only key columns for high-level relationship maps. Supports schema and table filtering (include/exclude, comma-separated). Designed for loading database context into an AI conversation. |
| `get_plantuml_diagram` | Generates a PlantUML ER diagram and saves it to a specified file path. Shows tables, columns, primary keys, and foreign key relationships with smart cardinality. Supports a `compact` mode that shows only key columns without data types. Supports schema and table filtering (include/exclude, comma-separated) and a configurable table limit (max 200). |
| `describe_table` | Returns comprehensive table metadata in Markdown: columns with data types, nullability, defaults, identity, computed expressions, indexes, foreign keys, and constraints. |
| `get_query_plan` | Returns the estimated or actual XML execution plan for a SELECT query. Estimated plans show the optimizer's plan without executing; actual plans include runtime statistics. |

### Progressive Discovery (requires `EnableDynamicToolsets: true`)

When `EnableDynamicToolsets` is enabled, the DBA toolsets (First Responder Kit, DarlingData, sp_WhoIsActive) are not loaded at startup. Instead, 3 lightweight discovery tools let the AI explore and enable toolsets on demand. This reduces initial context window usage — the AI only pays the token cost for toolsets it actually needs.

| Tool | Description |
|------|-------------|
| `list_toolsets` | Lists available DBA toolsets with their current status (available, enabled, or not configured) and tool counts. |
| `get_toolset_tools` | Returns detailed information about a specific toolset's tools and parameters before enabling it. |
| `enable_toolset` | Enables a toolset, making its tools available for use. Only works if the admin has enabled the toolset via the corresponding `Enable*` config flag. |

The existing `Enable*` config flags act as a security ceiling — `enable_toolset` will refuse to activate a toolset that the administrator has not allowed in `appsettings.json`. For example, if `EnableFirstResponderKit` is `false`, calling `enable_toolset("first_responder_kit")` returns an error.

**Example flow:**
1. AI calls `list_toolsets` — sees `first_responder_kit` is "available" (configured but not yet enabled)
2. AI calls `get_toolset_tools("first_responder_kit")` — reviews the 6 tools and their parameters
3. AI calls `enable_toolset("first_responder_kit")` — the 6 tools are now registered and usable
4. AI calls `sp_blitz` — runs the health check as normal

### First Responder Kit (requires `EnableFirstResponderKit: true`)

Requires the [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) to be installed on the target server.

| Tool | Description |
|------|-------------|
| `sp_blitz` | Overall SQL Server health check. Returns prioritized findings for performance, configuration, and security issues. |
| `sp_blitz_first` | Real-time performance diagnostics. Samples DMVs over an interval to identify current bottlenecks including waits, file latency, and perfmon counters. |
| `sp_blitz_cache` | Plan cache analysis. Identifies top queries by CPU, reads, duration, executions, or memory grants. |
| `sp_blitz_index` | Index analysis and tuning. Identifies missing, unused, and duplicate indexes with usage patterns. |
| `sp_blitz_who` | Active query monitor. Enhanced `sp_who`/`sp_who2` replacement showing what's running, with blocking info, tempdb usage, and query plans. |
| `sp_blitz_lock` | Deadlock analysis from the `system_health` extended event session. Shows victims, resources, and participating queries. |

### DarlingData (requires `EnableDarlingData: true`)

Requires the [DarlingData toolkit](https://github.com/erikdarling/DarlingData) to be installed on the target server.

| Tool | Description |
|------|-------------|
| `sp_pressure_detector` | Diagnoses CPU and memory pressure. Identifies resource bottlenecks, high-CPU queries, memory grants, and disk latency. |
| `sp_quickie_store` | Query Store analysis. Identifies top resource-consuming queries, plan regressions, and wait statistics. |
| `sp_health_parser` | Parses the `system_health` extended event session for historical diagnostics including waits, disk latency, CPU, memory, and locking events. |
| `sp_log_hunter` | Searches SQL Server error logs for errors, warnings, and custom messages. |
| `sp_human_events_block_viewer` | Analyzes blocking events captured by `sp_HumanEvents` sessions. Shows blocking chains, lock details, and wait information. |
| `sp_index_cleanup` | Finds unused and duplicate indexes that are candidates for removal based on usage statistics. |
| `sp_query_repro_builder` | Generates reproduction scripts for Query Store queries with parameter values to reproduce performance issues. |

### sp_WhoIsActive (requires `EnableWhoIsActive: true`)

Requires [sp_WhoIsActive](http://whoisactive.com/) to be installed on the target server.

| Tool | Description |
|------|-------------|
| `sp_whoisactive` | Monitors currently active sessions and queries with wait info, blocking details, tempdb usage, and resource consumption. |

## Security

### Query Validation

Query validation is the core security mechanism. Rather than using fragile regex patterns, the server parses every query into an AST using Microsoft's official `TSql170Parser` and enforces these rules:

- **Single statement only** — multiple statements are rejected
- **SELECT only** — INSERT, UPDATE, DELETE, DROP, EXEC, CREATE, ALTER, and all other statement types are blocked at the AST level
- **No SELECT INTO** — prevents table creation via SELECT
- **No external data access** — OPENROWSET, OPENQUERY, OPENDATASOURCE, and OPENXML are blocked
- **No linked servers** — four-part name references are rejected
- **No MAXRECURSION hint** — prevents overriding the default recursion limit (100) to protect against runaway recursive CTEs
- **Cross-database queries are allowed** — three-part names (e.g., `OtherDb.dbo.Table`) are permitted by design. The `databaseName` parameter on `read_data` sets the default database context for the connection, but it is **not a security boundary**. Queries can reference any database the SQL account has access to on that server. The security boundary is the server — cross-server access via linked servers is blocked.

Because validation operates on the parsed AST, it correctly handles edge cases that defeat string-based approaches: keywords inside comments, string literals, nested block comments, and encoding tricks.

To restrict access to a single database, configure the SQL account with permissions limited to that database only.

### Parameter Blocking for Diagnostic Tools

The DBA diagnostic tools (First Responder Kit, DarlingData, sp_WhoIsActive) execute stored procedures rather than ad-hoc SQL. To maintain read-only safety, the server blocks parameters that could cause these procedures to write data:

- **First Responder Kit** — blocks all `@Output*` parameters (e.g., `@OutputDatabaseName`, `@OutputTableName`, `@OutputTableNameFileStats`) that would write results to tables on the server
- **DarlingData** — blocks logging parameters (`@log_to_table`, `@log_database_name`, `@log_schema_name`, `@log_table_name_prefix`, `@log_retention_days`) and output parameters (`@output_database_name`, `@output_schema_name`, `@delete_retention_days`)
- **sp_WhoIsActive** — blocks `@destination_table`, `@return_schema`, `@schema`, and `@help`

Each service also enforces a procedure whitelist — only the specific procedures listed above can be executed.

### Rate Limiting

All tool executions are subject to rate limiting to prevent runaway queries from overwhelming the SQL Server:

- **Concurrency limiting** — at most `MaxConcurrentQueries` (default 5) queries execute simultaneously. Additional requests queue up to a limit, then are rejected.
- **Throughput limiting** — at most `MaxQueriesPerMinute` (default 60) queries are allowed per minute using a token bucket algorithm. Excess requests are rejected immediately.

Both limits apply across all tools (ad-hoc queries and stored procedure executions). Rejected requests return an error message asking the caller to wait and retry.

### Connection Security

Use Windows Authentication or Azure Managed Identity where possible to avoid storing credentials in configuration files. When SQL Authentication is required, use .NET's environment variable overrides (`__` separator) to inject credentials at runtime rather than storing passwords in `appsettings.json`. See [SECURITY.md](SECURITY.md) for detailed guidance including credential store integrations and connection string encryption.

### SQL Server Account

The SQL account should follow least-privilege principles: grant only `SELECT` permission, use a dedicated service account, and restrict database access to only what's needed. See [SECURITY.md](SECURITY.md) for the full recommendations including CLR assembly and Resource Governor guidance.

### Known Risks

- This project depends on the official Microsoft [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet package) which is currently a prerelease version. Prerelease packages may contain undiscovered security vulnerabilities and receive breaking changes. As the MCP framework handles all protocol I/O, any vulnerability in it directly affects this application's security boundary. Monitor the package for stable releases and upgrade when available.
- The data returned from a SQL Server query could include malicious prompt injection targeting AIs, this is a risk of all AI use and cannot be mitigated by this project, please ensure you're following best practices for AI security in your AI implementation and only connecting to trusted data sources.

## Architecture

*This section is primarily for contributors.*

The project follows a **two-layer design**: Tools → Services.

- **Tools** (`SqlServerMcp/Tools/`) — MCP endpoint definitions decorated with `[McpServerTool]`, auto-discovered at startup. Each tool is a thin wrapper that validates inputs and delegates to a service.
- **Services** (`SqlServerMcp/Services/`) — Business logic and database access, registered as singletons via DI. Nine service interfaces handle connection management, query execution, result serialization, rate limiting, and dynamic toolset management.
- **QueryValidator** (`SqlServerMcp/Services/QueryValidator.cs`) — Static AST-based query validator using `TSql170Parser` and the visitor pattern for security enforcement.

To add a new tool: create a class in `Tools/` with a static async method decorated with `[McpServerTool]`, inject services via method parameters, and it will be auto-registered at startup. See `CLAUDE.md` for detailed instructions.

## License

MIT
