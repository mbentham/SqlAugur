using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class GetSchemaOverviewTool
{
    private readonly ISchemaOverviewService _schemaOverviewService;
    private readonly IRateLimitingService _rateLimiter;

    public GetSchemaOverviewTool(ISchemaOverviewService schemaOverviewService, IRateLimitingService rateLimiter)
    {
        _schemaOverviewService = schemaOverviewService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "get_schema_overview",
        Title = "Get Database Schema Overview",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Markdown overview of database schema: tables, columns, types, PKs, FKs, unique/check constraints, defaults. Use get_plantuml_diagram for visual ER output or describe_table for single-table detail.")]
    public async Task<string> GetSchemaOverview(
        [Description("Server name from list_servers")] string serverName,
        [Description("Database name from list_databases")] string databaseName,
        [Description("Optional comma-separated schemas to include (e.g. 'dbo,sales'). Overrides excludeSchemas.")] string? includeSchemas = null,
        [Description("Optional comma-separated schemas to exclude (e.g. 'audit,staging'). Ignored if includeSchemas set.")] string? excludeSchemas = null,
        [Description("Optional comma-separated tables to include (e.g. 'Users,Orders'). Overrides excludeTables.")] string? includeTables = null,
        [Description("Optional comma-separated tables to exclude. Ignored if includeTables set.")] string? excludeTables = null,
        [Description("Max tables to include (1-200, default 50)")] int maxTables = 50,
        [Description("true/false. Show only PK/FK columns without data types")] bool compact = false,
        CancellationToken cancellationToken = default)
    {
        maxTables = Math.Clamp(maxTables, 1, 200);

        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _schemaOverviewService.GenerateOverviewAsync(
                serverName, databaseName,
                ToolHelper.ParseCommaSeparatedList(includeSchemas), ToolHelper.ParseCommaSeparatedList(excludeSchemas),
                ToolHelper.ParseCommaSeparatedList(includeTables), ToolHelper.ParseCommaSeparatedList(excludeTables),
                maxTables, cancellationToken, compact), cancellationToken);
    }
}
