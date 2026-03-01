using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class HealthParserTool
{
    private readonly IDarlingDataService _darlingDataService;
    private readonly IRateLimitingService _rateLimiter;

    public HealthParserTool(IDarlingDataService darlingDataService, IRateLimitingService rateLimiter)
    {
        _darlingDataService = darlingDataService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_health_parser",
        Title = "System Health Parser (sp_HealthParser)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_HealthParser to parse the system_health extended event session for historical diagnostics. Extracts waits, disk latency, CPU utilization, memory pressure, and locking events. Requires the DarlingData toolkit.")]
    public async Task<string> HealthParser(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("What to check: 'all' (default), 'waits', 'disk', 'cpu', 'memory', 'system', or 'locking'")]
        string? whatToCheck = null,
        [Description("Start date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? startDate = null,
        [Description("End date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? endDate = null,
        [Description("Only show warning-level events")]
        bool? warningsOnly = null,
        [Description("Filter to events in a specific database")]
        string? databaseName = null,
        [Description("Minimum wait duration in ms to include")]
        int? waitDurationMs = null,
        [Description("Round wait intervals to this many minutes")]
        int? waitRoundIntervalMinutes = null,
        [Description("Skip lock-related events")]
        bool? skipLocks = null,
        [Description("Threshold for pending task warnings")]
        int? pendingTaskThreshold = null,
        [Description("Include XML query plan columns in output (excluded by default to reduce response size)")]
        bool? includeQueryPlans = null,
        [Description("Include XML deadlock graph and report columns (excluded by default to reduce response size)")]
        bool? includeXmlReports = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _darlingDataService.ExecuteHealthParserAsync(
                serverName, whatToCheck, startDate, endDate, warningsOnly,
                databaseName, waitDurationMs, waitRoundIntervalMinutes,
                skipLocks, pendingTaskThreshold,
                includeQueryPlans, includeXmlReports, verbose, cancellationToken), cancellationToken);
    }
}
