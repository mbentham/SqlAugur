using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class LogHunterTool
{
    private readonly IDarlingDataService _darlingDataService;

    public LogHunterTool(IDarlingDataService darlingDataService)
    {
        _darlingDataService = darlingDataService;
    }

    [McpServerTool(
        Name = "sp_log_hunter",
        Title = "Error Log Search (sp_LogHunter)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_LogHunter to search SQL Server error logs for issues. Parses error logs for errors, warnings, and custom messages. Requires the DarlingData toolkit.")]
    public async Task<string> LogHunter(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Number of days back to search (default 7)")]
        int? daysBack = null,
        [Description("Start date for log search (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? startDate = null,
        [Description("End date for log search (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? endDate = null,
        [Description("Custom message text to search for in error logs")]
        string? customMessage = null,
        [Description("Only show entries matching the custom message")]
        bool? customMessageOnly = null,
        [Description("Only search the current (first) error log file")]
        bool? firstLogOnly = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _darlingDataService.ExecuteLogHunterAsync(
                serverName, daysBack, startDate, endDate,
                customMessage, customMessageOnly, firstLogOnly, cancellationToken));
    }
}
