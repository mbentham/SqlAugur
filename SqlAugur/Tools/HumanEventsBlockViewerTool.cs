using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class HumanEventsBlockViewerTool
{
    private readonly IDarlingDataService _darlingDataService;
    private readonly IRateLimitingService _rateLimiter;

    public HumanEventsBlockViewerTool(IDarlingDataService darlingDataService, IRateLimitingService rateLimiter)
    {
        _darlingDataService = darlingDataService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_human_events_block_viewer",
        Title = "Blocking Event Viewer (sp_HumanEventsBlockViewer)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_HumanEventsBlockViewer to analyze blocking events captured by sp_HumanEvents extended event sessions. Shows blocking chains, lock details, and wait information. Requires the DarlingData toolkit.")]
    public async Task<string> HumanEventsBlockViewer(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Name of the extended event session to read (default: keeper_HumanEvents_blocking)")]
        string? sessionName = null,
        [Description("Target type to read from: 'event_file' (default) or 'ring_buffer'")]
        string? targetType = null,
        [Description("Start date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? startDate = null,
        [Description("End date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? endDate = null,
        [Description("Filter to blocking events in a specific database")]
        string? databaseName = null,
        [Description("Filter to blocking events involving a specific object")]
        string? objectName = null,
        [Description("Maximum number of blocking events to return")]
        int? maxBlockingEvents = null,
        [Description("Include XML query plan columns in output (excluded by default to reduce response size)")]
        bool? includeQueryPlans = null,
        [Description("Include XML blocked process report columns (excluded by default to reduce response size)")]
        bool? includeXmlReports = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _darlingDataService.ExecuteHumanEventsBlockViewerAsync(
                serverName, sessionName, targetType, startDate, endDate,
                databaseName, objectName, maxBlockingEvents,
                includeQueryPlans, includeXmlReports, verbose, cancellationToken), cancellationToken);
    }
}
