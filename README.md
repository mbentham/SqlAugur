<!-- mcp-name: io.github.mbentham/sqlaugur -->
# SqlAugur

[![NuGet](https://img.shields.io/nuget/v/SqlAugur.svg)](https://www.nuget.org/packages/SqlAugur)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SqlAugur.svg)](https://www.nuget.org/packages/SqlAugur)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)

<a href="https://glama.ai/mcp/servers/@mbentham/sql-augur">
  <img width="380" height="200" src="https://glama.ai/mcp/servers/@mbentham/sql-augur/badge" />
</a>

**An MCP server that gives AI assistants safe, read-only access to SQL Server databases. Every query is parsed into a full AST using Microsoft's official T-SQL parser — not regex — so comment injection, string literal tricks, and encoding bypasses are blocked at the syntax level.**

```
┌──────────────┐          ┌───────────────────────────────────────────┐        ┌──────────────┐
│              │  stdio   │  SqlAugur                                 │        │              │
│  AI Client   │◄────────►│                                           │───────►│  SQL Server  │
│              │          │  ┌────────────┐  ┌──────────────────────┐ │        │              │
└──────────────┘          │  │  Query     │  │  Schema / Diagram /  │ │        └──────────────┘
                          │  │  Validator │  │  DBA Services        │ │
                          │  └────────────┘  └──────────────────────┘ │
                          │  ┌────────────────────────────────────┐   │
                          │  │  Rate Limiter                      │   │
                          │  └────────────────────────────────────┘   │
                          └───────────────────────────────────────────┘
```

## Quick Start

Prerequisite: [.NET 10.0 runtime](https://dotnet.microsoft.com/download)

**1. Install**

```bash
dotnet tool install -g SqlAugur
```

**2. Configure** — create `~/.config/sqlaugur/appsettings.json` (Linux/macOS) or `%APPDATA%\sqlaugur\appsettings.json` (Windows), setting the connection string for your environment:

```json
{
  "SqlAugur": {
    "Servers": {
      "production": {
        "ConnectionString": "Server=myserver;Database=master;Integrated Security=True;TrustServerCertificate=False;Encrypt=True;"
      }
    }
  }
}
```

**3. Connect** — add to your MCP client:

<details open>
<summary><strong>Claude Desktop</strong></summary>

Add to your [Claude Desktop config](https://modelcontextprotocol.io/quickstart/user) (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "sqlaugur": {
      "command": "sqlaugur"
    }
  }
}
```

</details>

<details>
<summary><strong>Claude Code</strong></summary>

```bash
claude mcp add --transport stdio sqlaugur -- sqlaugur
```

Or add to `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "sqlaugur": {
      "type": "stdio",
      "command": "sqlaugur"
    }
  }
}
```

</details>

<details>
<summary><strong>VS Code / Copilot</strong></summary>

Add to `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "sqlaugur": {
      "command": "sqlaugur"
    }
  }
}
```

</details>

**4. Verify** — ask your AI assistant to `list_servers` and you should see your configured connection.

For Docker, Podman, and other install methods, see [Installation](#installation).

## Why This Approach

- **AST-level query validation** — Most MCP database servers use keyword blocking or no validation at all. This project parses every query into a full syntax tree using Microsoft's official `TSql170Parser`. Comment injection, string literal tricks, and encoding bypasses are blocked at the syntax level, not with fragile regex patterns.

- **Rate limiting** — Token bucket throughput limiting and concurrency control prevent runaway AI query loops from overwhelming production SQL Servers. No other MCP database server offers this.

- **DBA diagnostic tooling** — Integrated support for First Responder Kit, DarlingData, and sp_WhoIsActive with parameter blocking that prevents write operations. This is an entirely new MCP capability category.

- **Response size optimisation** — DBA tools exclude verbose columns (XML query plans, deadlock graphs, metric breakdowns) and truncate long strings by default, reducing response sizes by 90–99%. Use `verbose` and `includeQueryPlans` parameters to get full untruncated output when needed.

- **Progressive discovery** — Up to 29 tools organized into toolsets that load on demand. Only 6 core tools are exposed initially, keeping the AI's context window small and reducing token usage. Additional toolsets are discovered and enabled as needed.

## Features

**Security**
- Read-only by design — only SELECT and CTE queries are permitted
- AST-based query validation using [ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom) (not regex)
- Parameter blocking on all diagnostic stored procedures to prevent writes
- Concurrency and throughput rate limiting

**Database Tooling**
- Multi-server support — named connections to multiple SQL Server instances
- Schema overview — concise Markdown schema maps with PKs, FKs, constraints, and defaults
- Table documentation — Markdown descriptions of columns, indexes, foreign keys, and constraints
- ER diagram generation — PlantUML and Mermaid diagrams with smart cardinality detection
- Schema exploration — list programmable objects, view definitions, extended properties, dependency graphs
- Query plan analysis — estimated or actual XML execution plans
- DBA diagnostics — optional integration with [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit), [DarlingData](https://github.com/erikdarlingdata/DarlingData), and [sp_WhoIsActive](https://github.com/amachanic/sp_whoisactive/) with automatic response size optimisation
- Progressive discovery — dynamic toolset mode reduces initial context window usage by exposing tools on demand

## Installation

All methods produce the same MCP server.

### NuGet Global Tool (recommended)

Prerequisite: [.NET 10.0 runtime](https://dotnet.microsoft.com/download)

```bash
dotnet tool install -g SqlAugur
```

Create your configuration file:

```bash
# Linux/macOS
mkdir -p ~/.config/sqlaugur
# Edit ~/.config/sqlaugur/appsettings.json with your server connections

# Windows (PowerShell)
mkdir "$env:APPDATA\sqlaugur" -Force
# Edit %APPDATA%\sqlaugur\appsettings.json with your server connections
```

MCP client configuration:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "sqlaugur"
    }
  }
}
```

To update: `dotnet tool update -g SqlAugur`

### Docker / Podman

```bash
# Volume-mount a config file
docker run -i --rm \
  -v /path/to/appsettings.json:/app/appsettings.json:ro,Z \
  ghcr.io/mbentham/sqlaugur:latest

# Or use environment variables (no config file needed)
docker run -i --rm \
  -e SqlAugur__Servers__production__ConnectionString="Server=host.docker.internal;Database=master;..." \
  ghcr.io/mbentham/sqlaugur:latest
```

> **Note:** To reach a SQL Server on the host machine, use `host.docker.internal` (Docker Desktop) or `--network=host` (Linux). Replace `docker` with `podman` — all commands are identical. The `:Z` flag on volume mounts is required for SELinux-enabled systems (Fedora, RHEL); Docker Desktop users on macOS/Windows can omit it.

MCP client configuration:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "docker",
      "args": ["run", "-i", "--rm",
        "-v", "/path/to/appsettings.json:/app/appsettings.json:ro,Z",
        "ghcr.io/mbentham/sqlaugur:latest"]
    }
  }
}
```

<details>
<summary>Docker Compose</summary>

```yaml
services:
  sqlaugur:
    image: ghcr.io/mbentham/sqlaugur:latest
    stdin_open: true
    volumes:
      - ./appsettings.json:/app/appsettings.json:ro,Z
```

MCP client configuration:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "docker",
      "args": ["compose", "run", "-i", "--rm", "sqlaugur"]
    }
  }
}
```

</details>


### Build from Source

Prerequisite: [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

```bash
git clone git@github.com:mbentham/SqlAugur.git
cd SqlAugur
dotnet publish SqlAugur -c Release -o SqlAugur/publish
cp SqlAugur/appsettings.example.json SqlAugur/publish/appsettings.json
# Edit SqlAugur/publish/appsettings.json with your server connections
```

MCP client configuration:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["/absolute/path/to/SqlAugur/publish/SqlAugur.dll"]
    }
  }
}
```

## Configuration

The server loads configuration from multiple sources. Higher-priority sources override lower ones:

1. **Command-line arguments**
2. **Environment variables** — using `__` as section delimiter (e.g., `SqlAugur__Servers__production__ConnectionString=...`)
3. **Current working directory** — `appsettings.json` in the directory you run the command from
4. **User config directory** — `~/.config/sqlaugur/appsettings.json` on Linux, `%APPDATA%\sqlaugur\appsettings.json` on Windows
5. **Azure Key Vault** — when `AzureKeyVaultUri` is set (see below)
6. **App directory** — `appsettings.json` next to the DLL

**Example configuration (Windows Authentication — recommended):**

```json
{
  "SqlAugur": {
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
| `EnableDynamicToolsets` | false | Enable progressive tool discovery — DBA tools load on demand via 3 meta-tools instead of at startup. Reduces initial context window usage. The `Enable*` flags still control which toolsets are allowed. |
| `AzureKeyVaultUri` | — | Azure Key Vault URI (e.g., `https://myvault.vault.azure.net/`). When set, secrets from the vault are added as a configuration source using [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential). Key Vault secret names use `--` as a section separator (e.g., a secret named `SqlAugur--Servers--prod--ConnectionString` maps to `SqlAugur:Servers:prod:ConnectionString`). |

> **Security Note:** `appsettings.json` is gitignored to prevent accidental credential commits. See [SECURITY.md](SECURITY.md) for recommended authentication methods including Windows Authentication, Azure Managed Identity, and secure credential storage options.

## Tools

The server provides 29 tools organized into toolsets. Six core tools are always available. Additional toolsets are loaded at startup (static mode) or on demand (dynamic mode).

### Core Tools

| Tool | Description |
|------|-------------|
| `list_servers` | Lists available SQL Server instances configured in `appsettings.json`. |
| `list_databases` | Lists all databases on a named server with names, IDs, states, and creation dates. |
| `read_data` | Executes a read-only SQL SELECT query. Only `SELECT` and `WITH` (CTE) queries are allowed. Results returned as JSON with a configurable row limit. |
| `get_query_plan` | Returns the estimated or actual XML execution plan for a SELECT query. |
| `get_schema_overview` | Concise Markdown schema overview: tables, columns, PKs, FKs, unique/check constraints, defaults. Supports `compact` mode, schema and table filtering. |
| `describe_table` | Comprehensive table metadata in Markdown: columns, data types, nullability, defaults, identity, computed expressions, indexes, FKs, constraints. |

<details open>
<summary><strong>Schema Exploration</strong> (4 tools)</summary>

| Tool | Description |
|------|-------------|
| `list_programmable_objects` | Lists views, stored procedures, functions, and triggers. Filterable by type and schema. |
| `get_object_definition` | Returns the source definition (CREATE statement) of a programmable object. |
| `get_extended_properties` | Reads extended properties (descriptions, metadata) on tables, columns, and other objects. |
| `get_object_dependencies` | Shows what an object references and what references it — upstream and downstream dependency graphs. |

</details>

<details open>
<summary><strong>Diagrams</strong> (2 tools)</summary>

| Tool | Description |
|------|-------------|
| `get_plantuml_diagram` | Generates a PlantUML ER diagram with tables, columns, PKs, and FK relationships. Saves to a `.puml` file. Supports `compact` mode, schema/table filtering, and a configurable table limit (max 200). |
| `get_mermaid_diagram` | Generates a Mermaid ER diagram with tables, columns, PKs, and FK relationships. Saves to a `.mmd` file. Supports `compact` mode, schema/table filtering, and a configurable table limit (max 200). |

</details>

### DBA Diagnostic Tools

Each toolkit is enabled independently via config flags and requires the corresponding stored procedures installed on the target SQL Server.

All DBA tools apply response size optimisation by default — XML query plan columns are excluded and long string values are truncated to keep responses within AI context window limits. Every tool supports these optional parameters:

| Parameter | Description |
|-----------|-------------|
| `verbose` | Return all columns with no truncation. |
| `includeQueryPlans` | Include XML execution plan columns in the output. |
| `maxRows` | Maximum rows to return per result set. Available on tools with variable-length output: BlitzIndex, BlitzLock, HealthParser, LogHunter (default 200), IndexCleanup, QueryReproBuilder. |

Some tools have additional parameters: `includeXmlReports` (BlitzLock, HealthParser, HumanEventsBlockViewer), `compact` (sp_WhoIsActive), `verboseMetrics` (QuickieStore).

<details>
<summary><strong>First Responder Kit</strong> (6 tools) — requires <code>EnableFirstResponderKit: true</code></summary>

Install from: [github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit)

| Tool | Description |
|------|-------------|
| `sp_blitz` | Overall SQL Server health check — prioritized findings for performance, configuration, and security. |
| `sp_blitz_first` | Real-time performance diagnostics — samples DMVs over an interval for waits, file latency, and perfmon counters. |
| `sp_blitz_cache` | Plan cache analysis — top queries by CPU, reads, duration, executions, or memory grants. |
| `sp_blitz_index` | Index analysis — missing, unused, and duplicate indexes with usage patterns. |
| `sp_blitz_who` | Active query monitor — what's running, blocking info, tempdb usage, query plans. |
| `sp_blitz_lock` | Deadlock analysis from the `system_health` extended event session. |

</details>

<details>
<summary><strong>DarlingData</strong> (7 tools) — requires <code>EnableDarlingData: true</code></summary>

Install from: [github.com/erikdarling/DarlingData](https://github.com/erikdarling/DarlingData)

| Tool | Description |
|------|-------------|
| `sp_pressure_detector` | Diagnoses CPU and memory pressure — resource bottlenecks, high-CPU queries, memory grants, disk latency. |
| `sp_quickie_store` | Query Store analysis — top resource-consuming queries, plan regressions, wait statistics. |
| `sp_health_parser` | Parses the `system_health` extended event session for historical waits, disk latency, CPU, memory, and locking. |
| `sp_log_hunter` | Searches SQL Server error logs for errors, warnings, and custom messages. |
| `sp_human_events_block_viewer` | Analyzes blocking events from `sp_HumanEvents` sessions — blocking chains, lock details, waits. |
| `sp_index_cleanup` | Finds unused and duplicate indexes that are candidates for removal. |
| `sp_query_repro_builder` | Generates reproduction scripts for Query Store queries with parameter values. |

</details>

<details>
<summary><strong>sp_WhoIsActive</strong> (1 tool) — requires <code>EnableWhoIsActive: true</code></summary>

Install from: [whoisactive.com](http://whoisactive.com/)

| Tool | Description |
|------|-------------|
| `sp_whoisactive` | Monitors active sessions and queries — wait info, blocking details, tempdb usage, resource consumption. |

</details>

### Progressive Discovery

When `EnableDynamicToolsets` is true, only core tools load at startup. Three meta-tools let the AI discover and enable additional toolsets on demand, reducing initial context window usage:

| Tool | Description |
|------|-------------|
| `list_toolsets` | Lists available toolsets with status (available, enabled, not configured) and tool counts. |
| `get_toolset_tools` | Returns detailed tool and parameter info for a specific toolset before enabling it. |
| `enable_toolset` | Enables a toolset, making its tools available. Only works if the admin has enabled the toolset via the corresponding `Enable*` config flag. |

**Example flow:**
1. AI calls `list_toolsets` — sees `first_responder_kit` is "available" (configured but not yet enabled)
2. AI calls `get_toolset_tools("first_responder_kit")` — reviews the 6 tools and their parameters
3. AI calls `enable_toolset("first_responder_kit")` — the 6 tools are now registered and usable
4. AI calls `sp_blitz` — runs the health check as normal

In static mode (`EnableDynamicToolsets: false`), all enabled toolsets load at startup and the discovery tools are not registered. Schema Exploration and Diagrams toolsets are always loaded regardless of mode.

> **Known limitation:** Progressive discovery relies on the MCP `notifications/tools/list_changed` notification to inform clients that new tools have been registered. Claude Code does not currently handle this notification ([anthropics/claude-code#4118](https://github.com/anthropics/claude-code/issues/4118)), so dynamically enabled toolsets will not appear. Use static mode (`EnableDynamicToolsets: false`) when using Claude Code.

## Security

### Query Validation

Every query is parsed into an [Abstract Syntax Tree](https://en.wikipedia.org/wiki/Abstract_syntax_tree) (AST) using Microsoft's official `TSql170Parser` and must pass these rules:

- **Single statement only** — multiple statements are rejected
- **SELECT only** — INSERT, UPDATE, DELETE, DROP, EXEC, CREATE, ALTER, and all other statement types are blocked
- **No SELECT INTO** — prevents table creation via SELECT
- **No external data access** — OPENROWSET, OPENQUERY, OPENDATASOURCE, OPENXML blocked
- **No linked servers** — four-part name references are rejected
- **No MAXRECURSION hint** — prevents overriding the default recursion limit
- **Cross-database queries are allowed** — three-part names work by design; the security boundary is the server, not the database. To restrict to a single database, limit the login's permissions.

Because validation operates on the parsed AST, it correctly handles edge cases that defeat string-based approaches: keywords inside comments, string literals, nested block comments, and encoding tricks.

### Parameter Blocking

Diagnostic stored procedures execute via whitelisted procedure names with blocked parameters that prevent writes:

- **First Responder Kit** — all `@Output*` parameters blocked (prevents writing results to server tables)
- **DarlingData** — logging and output parameters blocked (prevents table creation and data retention)
- **sp_WhoIsActive** — `@destination_table`, `@return_schema`, `@schema`, `@help` blocked

### Rate Limiting

All tool executions are subject to concurrency limiting (`MaxConcurrentQueries`, default 5) and throughput limiting (`MaxQueriesPerMinute`, default 60). Excess requests are rejected with a retry message.

### Connection Security

Use Windows Authentication or Azure Managed Identity where possible to avoid storing credentials in config files. When SQL Authentication is required, use environment variable overrides to inject credentials at runtime. See [SECURITY.md](SECURITY.md) for detailed guidance including credential stores and connection string encryption.

### Known Risks

- This project depends on the official Microsoft [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet package) which is currently a prerelease version. Prerelease packages may contain undiscovered security vulnerabilities and receive breaking changes. As the MCP framework handles all protocol I/O, any vulnerability in it directly affects this application's security boundary. Monitor the package for stable releases and upgrade when available.
- The data returned from a SQL Server query could include malicious prompt injection targeting AIs. This is a risk of all AI use and cannot be mitigated by this project. Ensure you're following best practices for AI security and only connecting to trusted data sources.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for architecture details, development setup, testing instructions, and guidelines for adding new tools.

## License

[MIT](LICENSE)
