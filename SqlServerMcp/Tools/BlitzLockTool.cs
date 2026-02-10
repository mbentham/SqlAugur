using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class BlitzLockTool
{
    private readonly IFirstResponderService _frkService;

    public BlitzLockTool(IFirstResponderService frkService)
    {
        _frkService = frkService;
    }

    [McpServerTool(
        Name = "sp_blitz_lock",
        Title = "Deadlock Analysis (sp_BlitzLock)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_BlitzLock to analyze deadlocks captured by the system_health extended event session. Shows deadlock victims, resources, and participating queries. Requires the First Responder Kit.")]
    public async Task<string> BlitzLock(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Filter to deadlocks in a specific database")]
        string? databaseName = null,
        [Description("Start date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        string? startDate = null,
        [Description("End date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        string? endDate = null,
        [Description("Filter to deadlocks involving a specific object (e.g. 'DatabaseName.SchemaName.TableName')")]
        string? objectName = null,
        [Description("Filter to deadlocks involving a specific stored procedure")]
        string? storedProcName = null,
        [Description("Filter by application name")]
        string? appName = null,
        [Description("Filter by host name")]
        string? hostName = null,
        [Description("Filter by login name")]
        string? loginName = null,
        [Description("Show only deadlock victims (true) or all participants (false)")]
        bool? victimsOnly = null,
        [Description("Name of the extended event session to read from (default: system_health)")]
        string? eventSessionName = null,
        CancellationToken cancellationToken = default)
    {
        DateTime? parsedStart = null;
        DateTime? parsedEnd = null;

        if (startDate is not null)
        {
            if (!DateTime.TryParse(startDate, out var s))
                throw new McpException($"Invalid start date format: '{startDate}'. Use yyyy-MM-dd or yyyy-MM-dd HH:mm:ss.");
            parsedStart = s;
        }

        if (endDate is not null)
        {
            if (!DateTime.TryParse(endDate, out var e))
                throw new McpException($"Invalid end date format: '{endDate}'. Use yyyy-MM-dd or yyyy-MM-dd HH:mm:ss.");
            parsedEnd = e;
        }

        return await ToolHelper.ExecuteAsync(() =>
            _frkService.ExecuteBlitzLockAsync(
                serverName, databaseName, parsedStart, parsedEnd,
                objectName, storedProcName, appName, hostName,
                loginName, victimsOnly, eventSessionName, cancellationToken));
    }
}
