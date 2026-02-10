using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class WhoIsActiveTool
{
    private readonly IWhoIsActiveService _whoIsActiveService;

    public WhoIsActiveTool(IWhoIsActiveService whoIsActiveService)
    {
        _whoIsActiveService = whoIsActiveService;
    }

    [McpServerTool(
        Name = "sp_whoisactive",
        Title = "Active Session Monitor (sp_WhoIsActive)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_WhoIsActive to monitor currently active sessions and queries. Shows running queries with wait info, blocking details, tempdb usage, and resource consumption. Requires sp_WhoIsActive to be installed.")]
    public async Task<string> WhoIsActive(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Filter sessions by text (matched against login, host, program, or database name depending on filter_type)")]
        string? filter = null,
        [Description("Type of filter: 'session' (default), 'program', 'database', 'login', 'host'")]
        string? filterType = null,
        [Description("Exclude sessions matching this text")]
        string? notFilter = null,
        [Description("Type of not_filter: 'session' (default), 'program', 'database', 'login', 'host'")]
        string? notFilterType = null,
        [Description("Include the current session in results")]
        bool? showOwnSpid = null,
        [Description("Include system sessions in results")]
        bool? showSystemSpids = null,
        [Description("Show sleeping sessions: 0=no (default), 1=only sleeping with open transactions, 2=all sleeping")]
        int? showSleepingSpids = null,
        [Description("Get full text of inner-most running statement")]
        bool? getFullInnerText = null,
        [Description("Get query plans: 0=none (default), 1=plan for running statement, 2=full batch plan")]
        int? getPlans = null,
        [Description("Get the outer SQL command (full batch text)")]
        bool? getOuterCommand = null,
        [Description("Get transaction info including log usage")]
        bool? getTransactionInfo = null,
        [Description("Get task-level detail: 0=none, 1=summary (default), 2=detailed")]
        int? getTaskInfo = null,
        [Description("Get lock information for each session")]
        bool? getLocks = null,
        [Description("Get average elapsed time from query stats")]
        bool? getAvgTime = null,
        [Description("Get additional session info (client info, isolation level, etc.)")]
        bool? getAdditionalInfo = null,
        [Description("Get memory grant and usage information")]
        bool? getMemoryInfo = null,
        [Description("Find and report blocking chain leaders")]
        bool? findBlockLeaders = null,
        [Description("Seconds between two samples for delta mode (0-15, 0=disabled). Takes two snapshots and shows the difference.")]
        int? deltaInterval = null,
        [Description("Sort order: '[start_time] ASC' (default), '[cpu] DESC', '[reads] DESC', '[writes] DESC', '[tempdb_current] DESC', '[blocking_session_id] DESC'")]
        string? sortOrder = null,
        [Description("Format output for readability (adds units to numeric values)")]
        bool? formatOutput = null,
        CancellationToken cancellationToken = default)
    {
        if (deltaInterval.HasValue)
            deltaInterval = Math.Clamp(deltaInterval.Value, 0, 15);

        return await ToolHelper.ExecuteAsync(() =>
            _whoIsActiveService.ExecuteWhoIsActiveAsync(
                serverName, filter, filterType, notFilter, notFilterType,
                showOwnSpid, showSystemSpids, showSleepingSpids,
                getFullInnerText, getPlans, getOuterCommand, getTransactionInfo,
                getTaskInfo, getLocks, getAvgTime, getAdditionalInfo,
                getMemoryInfo, findBlockLeaders, deltaInterval, sortOrder,
                formatOutput, cancellationToken));
    }
}
