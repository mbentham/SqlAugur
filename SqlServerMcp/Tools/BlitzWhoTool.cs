using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class BlitzWhoTool
{
    private readonly IFirstResponderService _frkService;

    public BlitzWhoTool(IFirstResponderService frkService)
    {
        _frkService = frkService;
    }

    [McpServerTool(
        Name = "sp_blitz_who",
        Title = "Active Query Monitor (sp_BlitzWho)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_BlitzWho to monitor currently active queries. Enhanced replacement for sp_who/sp_who2 showing what's running now, with blocking info, tempdb usage, and query plans. Requires the First Responder Kit.")]
    public async Task<string> BlitzWho(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Show detailed expert-level output")]
        bool? expertMode = null,
        [Description("Include sleeping/idle sessions")]
        bool? showSleepingSpids = null,
        [Description("Only show queries running longer than this many seconds")]
        int? minElapsedSeconds = null,
        [Description("Only show queries with at least this much CPU time (ms)")]
        int? minCpuTime = null,
        [Description("Only show queries with at least this many logical reads")]
        int? minLogicalReads = null,
        [Description("Only show sessions blocking others for at least this many seconds")]
        int? minBlockingSeconds = null,
        [Description("Only show sessions using at least this much tempdb (MB)")]
        int? minTempdbMb = null,
        [Description("Show actual parameter values for running queries")]
        bool? showActualParameters = null,
        [Description("Attempt to capture live query plans (SQL Server 2016+)")]
        bool? getLiveQueryPlan = null,
        [Description("Sort results by: elapsed_time, cpu, reads, writes, tempdb, blocking")]
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _frkService.ExecuteBlitzWhoAsync(
                serverName, expertMode, showSleepingSpids,
                minElapsedSeconds, minCpuTime, minLogicalReads,
                minBlockingSeconds, minTempdbMb, showActualParameters,
                getLiveQueryPlan, sortOrder, cancellationToken));
    }
}
