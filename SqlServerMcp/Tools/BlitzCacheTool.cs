using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class BlitzCacheTool
{
    private readonly IFirstResponderService _frkService;

    public BlitzCacheTool(IFirstResponderService frkService)
    {
        _frkService = frkService;
    }

    [McpServerTool(
        Name = "sp_blitz_cache",
        Title = "Plan Cache Analysis (sp_BlitzCache)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_BlitzCache to analyze the plan cache for resource-intensive queries. Identifies top queries by CPU, reads, duration, executions, or memory grants. Requires the First Responder Kit.")]
    public async Task<string> BlitzCache(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Sort results by: cpu, reads, duration, executions, xpm (executions per minute), memory grant, writes, recent compilations, spills, all. Default: cpu")]
        string? sortOrder = null,
        [Description("Number of top queries to return (default 10, max 100)")]
        int? top = null,
        [Description("Show detailed expert-level output")]
        bool? expertMode = null,
        [Description("Filter to a specific database name")]
        string? databaseName = null,
        [Description("Search query plans for this text (slow - searches XML). Use sparingly.")]
        string? slowlySearchPlansFor = null,
        [Description("Format output for Excel export (removes XML columns)")]
        bool? exportToExcel = null,
        CancellationToken cancellationToken = default)
    {
        if (top.HasValue)
            top = Math.Clamp(top.Value, 1, 100);

        return await ToolHelper.ExecuteAsync(() =>
            _frkService.ExecuteBlitzCacheAsync(
                serverName, sortOrder, top, expertMode, databaseName,
                slowlySearchPlansFor, exportToExcel, cancellationToken));
    }
}
