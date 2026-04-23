# Contributing

Contributions are welcome! This guide covers the project architecture, development setup, and conventions.

## Getting Started

**Prerequisites:**
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Podman](https://podman.io/) (for integration tests only)

**Clone and build:**

```bash
git clone git@github.com:mbentham/SqlAugur.git
cd SqlAugur
dotnet build
```

## Architecture

The project follows a **two-layer design**: Tools → Services.

```
SqlAugur/
├── Configuration/
│   ├── SqlAugurOptions.cs            # Options bound from appsettings.json
│   ├── SqlAugurOptionsValidator.cs
│   └── ToolRegistry.cs               # Tool groupings (core, schema, diagrams, DBA)
├── Services/
│   ├── QueryValidator.cs             # AST-based T-SQL validation
│   ├── SqlServerService.cs           # Query execution, server/database listing
│   ├── DiagramService.cs             # PlantUML and Mermaid ER diagrams
│   ├── SchemaOverviewService.cs      # Markdown schema overviews
│   ├── TableDescribeService.cs       # Markdown table documentation
│   ├── SchemaExplorationService.cs   # Programmable objects, definitions, dependencies
│   ├── RateLimitingService.cs        # Concurrency + throughput rate limiting
│   ├── FirstResponderService.cs      # First Responder Kit wrapper
│   ├── DarlingDataService.cs         # DarlingData wrapper
│   ├── WhoIsActiveService.cs         # sp_WhoIsActive wrapper
│   ├── ToolsetManager.cs             # Dynamic toolset discovery and loading
│   └── StoredProcedureServiceBase.cs # Base class for DBA tool services
├── Tools/                            # MCP endpoint definitions (thin wrappers)
└── Program.cs                        # Host setup, DI, config, MCP server
```

**Tools** (`SqlAugur/Tools/`) are MCP endpoint definitions decorated with `[McpServerTool]`, auto-discovered at startup via `WithToolsFromAssembly()`. Each tool is a thin wrapper that validates inputs and delegates to a service.

**Services** (`SqlAugur/Services/`) contain all business logic and database access, registered as singletons via DI in `Program.cs`.

### Key Components

- **QueryValidator** — Static class using `TSql180Parser` to parse queries into a full AST. Uses the visitor pattern (`ForbiddenNodeVisitor`) to reject non-SELECT statements, SELECT INTO, external data access (OPENROWSET and its Cosmos/internal variants, OPENQUERY, OPENDATASOURCE, OPENXML), linked server references, and MAXRECURSION hints. This is the core security mechanism — AST-based validation, not regex.

- **ToolRegistry** — Defines tool groupings: Core (6), Schema Exploration (4), Diagrams (2), First Responder Kit (6), DarlingData (7), WhoIsActive (1), Discovery (3). `GetToolTypes()` returns the appropriate set based on config flags and dynamic mode.

- **ToolsetManager** — Manages dynamic toolset discovery and loading. Maintains a frozen dictionary of toolset definitions with config predicates. Supports listing, inspecting, and enabling toolsets at runtime.

- **StoredProcedureServiceBase** — Abstract base class for DBA tool services. Enforces procedure whitelists and parameter blocking (preventing write operations through output/logging parameters).

- **RateLimitingService** — Wraps `System.Threading.RateLimiting` to enforce concurrent query limits and per-minute throughput limits across all tool executions.

### Design Decisions

- **AST-based query validation, not regex** — The official T-SQL parser correctly handles comments, string literals, nested block comments, and encoding tricks that regex approaches miss. Test coverage specifically targets bypass attempts.
- **Stdout is reserved for MCP protocol** — All logging is routed to stderr via `LogToStandardErrorThreshold = LogLevel.Trace`.
- **Progressive disclosure** — Tools are grouped into toolsets. In dynamic mode, only core tools load initially; additional toolsets are discovered and enabled on-demand to keep the AI's tool count manageable.

## Development

### Build and Run

```bash
# Build
dotnet build

# Run (starts MCP server on stdio)
dotnet run --project SqlAugur

# Publish
dotnet publish SqlAugur -c Release -o SqlAugur/publish

# Pack as NuGet global tool
dotnet pack SqlAugur -c Release
```

Use `dotnet build` or `dotnet test` from the repo root — the `SqlAugur.slnx` solution file includes all three projects.

### Unit Tests

No external dependencies required:

```bash
dotnet test SqlAugur.Tests

# Run a specific test by name
dotnet test --filter "DisplayName~TestNameHere"

# Run tests in a specific class
dotnet test --filter "FullyQualifiedName~QueryValidatorTests"
```

### Integration Tests

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up a SQL Server 2025 container via Podman (rootless). A single container is shared across all test classes via an xUnit collection fixture.

**Prerequisites:**
- Podman installed with user socket active: `systemctl --user start podman.socket`
- First run pulls `mcr.microsoft.com/mssql/server:2025-latest` (~1.5 GB)

```bash
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test SqlAugur.IntegrationTests
```

Run all tests (unit + integration):

```bash
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test
```

## Adding a New Tool

1. Create a class in `SqlAugur/Tools/` with a static async method decorated with `[McpServerTool]`
2. Use `[Description]` attributes on parameters for MCP schema documentation
3. Inject services via method parameters (DI-resolved automatically)
4. Convert domain exceptions to `McpException`
5. The tool is auto-registered at startup — no manual wiring needed
6. Add the tool type to the appropriate array in `ToolRegistry.cs` (core, schema exploration, diagrams, or a DBA toolset)
7. If creating a new toolset, add an entry to `ToolsetManager.Toolsets`

## Pre-Commit Checks

Before every commit, run these package health checks and resolve any issues before proceeding:

```bash
# Check for packages with known security vulnerabilities
dotnet list package --vulnerable

# Check for deprecated packages
dotnet list package --deprecated

# Check for outdated packages
dotnet list package --outdated
```

If any command reports issues, update the affected packages and verify the build and tests still pass before committing.

## Pull Requests

- Run the full test suite before submitting
- Follow existing code patterns — the codebase favors thin tools delegating to services
- Include tests for new functionality (security-focused tests for any validation changes)
- Keep PRs focused on a single concern
