using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class BlitzFirstTool
{
    private readonly IFirstResponderService _frkService;

    public BlitzFirstTool(IFirstResponderService frkService)
    {
        _frkService = frkService;
    }

    [McpServerTool(
        Name = "sp_blitz_first",
        Title = "Real-time Performance (sp_BlitzFirst)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_BlitzFirst for real-time performance diagnostics. Samples DMVs over an interval (default 5 seconds) to identify current bottlenecks including waits, file latency, and perfmon counters. Requires the First Responder Kit.")]
    public async Task<string> BlitzFirst(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Number of seconds to sample (default 5, max 60). Higher values give more accurate readings.")]
        int? seconds = null,
        [Description("Show detailed expert-level output with additional result sets")]
        bool? expertMode = null,
        [Description("Include sleeping/idle sessions in the output")]
        bool? showSleepingSpids = null,
        [Description("Show cumulative stats since server startup instead of sampling")]
        bool? sinceStartup = null,
        [Description("Threshold in ms for flagging file latency issues (default 100)")]
        int? fileLatencyThresholdMs = null,
        CancellationToken cancellationToken = default)
    {
        if (seconds.HasValue)
            seconds = Math.Clamp(seconds.Value, 1, 60);

        return await ToolHelper.ExecuteAsync(() =>
            _frkService.ExecuteBlitzFirstAsync(
                serverName, seconds, expertMode, showSleepingSpids,
                sinceStartup, fileLatencyThresholdMs, cancellationToken));
    }
}
