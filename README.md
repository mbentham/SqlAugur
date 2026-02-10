# SQL Server MCP for DBAs

A .NET MCP (Model Context Protocol) server that gives AI assistants safe, read-only access to SQL Server databases. Includes integrated DBA diagnostic tooling from the First Responder Kit, DarlingData, and sp_WhoIsActive.

## Features

- **Read-only by design** — only SELECT and CTE queries are permitted
- **AST-based query validation** — uses the official Microsoft T-SQL parser ([ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom)) to validate queries at the syntax tree level, not with regex
- **Multi-server support** — configure named connections to multiple SQL Server instances
- **ER diagram generation** — produces PlantUML diagrams with smart cardinality detection
- **Table documentation** — generates detailed Markdown descriptions of table structure, indexes, and constraints
- **Query plan analysis** — retrieve estimated or actual XML execution plans for SELECT queries
- **DBA diagnostic tooling** — optional integration with industry-standard SQL Server diagnostic tools:
  - [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) — The sp_Blitz tools - health checks, performance diagnostics, index analysis, deadlock investigation
  - [DarlingData toolkit](https://github.com/erikdarling/DarlingData) — CPU/memory pressure detection, Query Store analysis, error log search, blocking analysis
  - [sp_WhoIsActive](http://whoisactive.com/) — real-time session and query monitoring

## Prerequisites

**Required:**
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Access to one or more SQL Server instances

**Optional (for DBA tools):**

The following must be installed on the target SQL Server instances to use the corresponding tools. They are only needed if you enable `EnableDbaTools`.

- [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) — for `sp_blitz`, `sp_blitz_first`, `sp_blitz_cache`, `sp_blitz_index`, `sp_blitz_who`, `sp_blitz_lock`
- [DarlingData toolkit](https://github.com/erikdarling/DarlingData) — for `sp_pressure_detector`, `sp_quickie_store`, `sp_health_parser`, `sp_log_hunter`, `sp_human_events_block_viewer`, `sp_index_cleanup`, `sp_query_repro_builder`
- [sp_WhoIsActive](http://whoisactive.com/) — for `sp_whoisactive`

## Configuration

Copy the example configuration and edit it with your SQL Server connections:

```bash
cp SqlServerMcp/appsettings.example.json SqlServerMcp/appsettings.json
```

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
    "EnableDbaTools": false
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Servers` | — | Named SQL Server connections (name → connection string) |
| `MaxRows` | 1000 | Maximum rows returned per query |
| `CommandTimeoutSeconds` | 30 | SQL command timeout for all queries and procedures |
| `EnableDbaTools` | false | Enable DBA diagnostic tools (First Responder Kit, DarlingData, sp_WhoIsActive) |

> **Security Note:** `appsettings.json` is gitignored to prevent accidental credential commits. See the [Connection Security](#connection-security-and-credential-management) section for recommended authentication methods including Windows Authentication, Azure Managed Identity, and secure credential storage options.

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project SqlServerMcp

# Run tests
dotnet test
```

## MCP Client Setup

### Claude Desktop

Add to your Claude Desktop configuration (`claude_desktop_config.json`):

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

### Claude Code

Add to your Claude Code MCP settings:

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

The server exposes 20 tools: 5 core tools that are always available, and 15 DBA tools that require `EnableDbaTools: true`.

### Core Tools

#### `list_servers`

Lists the available SQL Server instances configured in `appsettings.json`. Call this first to discover server names.

#### `list_databases`

Lists all databases on a named server, returning database names, IDs, states, and creation dates.

#### `read_data`

Executes a read-only SQL SELECT query against a specific database. Only `SELECT` and `WITH` (CTE) queries are allowed. Results are returned as JSON with a configurable row limit.

#### `get_diagram`

Generates a PlantUML ER diagram for a database showing tables, columns, primary keys, and foreign key relationships. Supports optional schema filtering and a configurable table limit (max 200).

#### `describe_table`

Returns comprehensive metadata about a single table in Markdown format, including columns with data types, nullability, defaults, identity properties, computed expressions, indexes, foreign keys, check constraints, and default constraints.

### DBA Tools (requires `EnableDbaTools: true`)

#### Query Analysis

**`get_query_plan`** — Returns the estimated or actual XML execution plan for a SELECT query. Estimated plans show the optimizer's plan without executing. Actual plans execute the query and include runtime statistics. Uses the same query validation as `read_data`.

#### First Responder Kit

Requires the [First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) to be installed on the target server.

**`sp_blitz`** — Overall SQL Server health check. Returns prioritized findings including performance, configuration, and security issues.

**`sp_blitz_first`** — Real-time performance diagnostics. Samples DMVs over an interval to identify current bottlenecks including waits, file latency, and perfmon counters.

**`sp_blitz_cache`** — Plan cache analysis. Identifies top queries by CPU, reads, duration, executions, or memory grants.

**`sp_blitz_index`** — Index analysis and tuning recommendations. Identifies missing, unused, and duplicate indexes with usage patterns.

**`sp_blitz_who`** — Active query monitor. Enhanced replacement for `sp_who`/`sp_who2` showing what's running now, with blocking info, tempdb usage, and query plans.

**`sp_blitz_lock`** — Deadlock analysis from the `system_health` extended event session. Shows deadlock victims, resources, and participating queries.

#### DarlingData

Requires the [DarlingData toolkit](https://github.com/erikdarling/DarlingData) to be installed on the target server.

**`sp_pressure_detector`** — Diagnoses CPU and memory pressure. Identifies resource bottlenecks, high-CPU queries, memory grants, and disk latency.

**`sp_quickie_store`** — Query Store analysis. Identifies top resource-consuming queries, plan regressions, and wait statistics from Query Store.

**`sp_health_parser`** — Parses the `system_health` extended event session for historical diagnostics including waits, disk latency, CPU utilization, memory pressure, and locking events.

**`sp_log_hunter`** — Searches SQL Server error logs for errors, warnings, and custom messages.

**`sp_human_events_block_viewer`** — Analyzes blocking events captured by `sp_HumanEvents` extended event sessions. Shows blocking chains, lock details, and wait information.

**`sp_index_cleanup`** — Finds unused and duplicate indexes that are candidates for removal. Analyzes index usage statistics to identify indexes with low reads and high write overhead.

**`sp_query_repro_builder`** — Generates reproduction scripts for Query Store queries. Creates executable scripts with parameter values to reproduce query performance issues.

#### sp_WhoIsActive

Requires [sp_WhoIsActive](http://whoisactive.com/) to be installed on the target server.

**`sp_whoisactive`** — Monitors currently active sessions and queries. Shows running queries with wait info, blocking details, tempdb usage, and resource consumption.

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

### Connection Security and Credential Management

**Recommended: Use Windows Authentication or Azure Managed Identity**

The most secure authentication methods avoid storing credentials in configuration files entirely:

**Windows Authentication (on-premises or domain-joined environments):**
```json
{
  "SqlServerMcp": {
    "Servers": {
      "production": {
        "ConnectionString": "Server=myserver;Database=master;Integrated Security=True;TrustServerCertificate=False;Encrypt=True;"
      }
    }
  }
}
```

**Azure Managed Identity (Azure SQL Database):**
```json
{
  "SqlServerMcp": {
    "Servers": {
      "azure-prod": {
        "ConnectionString": "Server=myserver.database.windows.net;Database=master;Authentication=Active Directory Managed Identity;TrustServerCertificate=False;Encrypt=True;"
      }
    }
  }
}
```

**If SQL Authentication is Required:**

When Windows Authentication or Managed Identity are not available, follow these practices:

1. **Never commit credentials to source control** — `appsettings.json` is already gitignored, but ensure you never commit credentials in example files or documentation

2. **Use environment variables or secrets management:**

   ```json
   {
     "SqlServerMcp": {
       "Servers": {
         "production": {
           "ConnectionString": "Server=myserver;Database=master;User Id=sa;Password=${SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;"
         }
       }
     }
   }
   ```

   Then set the environment variable before running:
   ```bash
   export SQL_PASSWORD="your-secure-password"
   dotnet run --project SqlServerMcp
   ```

3. **Use secure credential stores:**
   - **Azure Key Vault** — for Azure deployments, integrate with `Azure.Extensions.AspNetCore.Configuration.Secrets`
   - **AWS Secrets Manager** — for AWS deployments, use the AWS SDK to retrieve secrets
   - **HashiCorp Vault** — for on-premises, use Vault for centralized secrets management
   - **Windows Credential Manager** — for local development on Windows

4. **Rotate credentials regularly** — if using SQL authentication, implement a credential rotation policy (e.g., every 90 days)

5. **Use strong passwords** — if SQL authentication is required, use passwords with:
   - Minimum 16 characters
   - Mix of uppercase, lowercase, numbers, and special characters
   - Generated randomly (not dictionary words or patterns)

**Connection String Encryption:**

Always use encrypted connections to protect credentials in transit:
- Set `Encrypt=True` in all connection strings
- Use `TrustServerCertificate=False` for production (only use `True` for development with self-signed certificates)
- Ensure SQL Server has a valid SSL/TLS certificate from a trusted CA

### SQL Server Account Recommendations

The SQL account used by this MCP server should follow least-privilege principles:

- **Grant read-only access** — the account only needs `SELECT` permission on the databases and schemas it should access. Do not grant `db_datawriter`, `db_ddladmin`, or server-level roles like `sysadmin`.
- **Do not grant EXECUTE on unsafe CLR assemblies** — `SELECT` statements can call user-defined functions, including CLR functions. If a CLR assembly is registered with `EXTERNAL_ACCESS` or `UNSAFE` permission sets, it can perform file I/O, network calls, and other side effects when invoked from a SELECT. The service account should not have EXECUTE permission on any such assemblies.
- **Use a dedicated service account** — do not reuse accounts shared with other applications. A dedicated account makes it easy to audit activity and revoke access independently.
- **Restrict database access** — if the account should only query specific databases, grant access only to those databases. Three-part name queries (`OtherDb.dbo.Table`) are allowed by design, so database-level permissions are the control point.
- **Consider Resource Governor** — for production SQL Server instances, place the service account in a Resource Governor workload group with CPU and memory limits to prevent expensive queries from impacting other workloads.

### Known Risks

- This project is built using the official Microsoft [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) which is still a preview release
- The data returned from a SQL Server query could include malicious prompt injection targetting AIs, this is a risk of all AI use and cannot be mitigated by this project, please ensure you're following best practices for AI security in your AI implementation and only connecting to trusted data sources.

## Architecture

The project follows a **two-layer design**: Tools → Services.

- **Tools** (`SqlServerMcp/Tools/`) — MCP endpoint definitions decorated with `[McpServerTool]`, auto-discovered at startup. Each tool is a thin wrapper that validates inputs and delegates to a service.
- **Services** (`SqlServerMcp/Services/`) — Business logic and database access, registered as singletons via DI. Services handle connection management, query execution, and result serialization.
- **QueryValidator** (`SqlServerMcp/Services/QueryValidator.cs`) — Static AST-based query validator using `TSql170Parser` and the visitor pattern for security enforcement.

To add a new tool: create a class in `Tools/` with a static async method decorated with `[McpServerTool]`, inject services via method parameters, and it will be auto-registered at startup. See `CLAUDE.md` for detailed instructions.

## License

MIT
