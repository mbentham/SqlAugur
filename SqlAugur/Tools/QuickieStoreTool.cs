using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class QuickieStoreTool
{
    private readonly IDarlingDataService _darlingDataService;
    private readonly IRateLimitingService _rateLimiter;

    public QuickieStoreTool(IDarlingDataService darlingDataService, IRateLimitingService rateLimiter)
    {
        _darlingDataService = darlingDataService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_quickie_store",
        Title = "Query Store Analysis (sp_QuickieStore)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_QuickieStore to analyze Query Store data for performance issues. Identifies top resource-consuming queries, plan regressions, and wait statistics from Query Store. Requires the DarlingData toolkit.")]
    public async Task<string> QuickieStore(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Database to analyze (required unless @get_all_databases is true)")]
        string? databaseName = null,
        [Description("Sort results by: cpu, reads, writes, duration, executions, memory, tempdb, spills, avg cpu, avg reads, avg writes, avg duration, avg executions, avg memory, avg tempdb, avg spills")]
        string? sortOrder = null,
        [Description("Number of top queries to return (1-100, default 10)")]
        int? top = null,
        [Description("Start date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? startDate = null,
        [Description("End date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? endDate = null,
        [Description("Minimum execution count filter")]
        int? executionCount = null,
        [Description("Minimum duration in milliseconds filter")]
        int? durationMs = null,
        [Description("Filter to a specific procedure schema")]
        string? procedureSchema = null,
        [Description("Filter to a specific procedure name")]
        string? procedureName = null,
        [Description("Comma-separated list of query IDs to include")]
        string? includeQueryIds = null,
        [Description("Comma-separated list of query hashes to include")]
        string? includeQueryHashes = null,
        [Description("Comma-separated list of plan IDs to ignore")]
        string? ignorePlanIds = null,
        [Description("Comma-separated list of query IDs to ignore")]
        string? ignoreQueryIds = null,
        [Description("Search for queries containing this text")]
        string? queryTextSearch = null,
        [Description("Exclude queries containing this text")]
        string? queryTextSearchNot = null,
        [Description("Filter by wait type (e.g. 'CXPACKET', 'PAGEIOLATCH')")]
        string? waitFilter = null,
        [Description("Filter by query type (e.g. 'SELECT', 'INSERT', 'UPDATE', 'DELETE')")]
        string? queryType = null,
        [Description("Show detailed expert-level output")]
        bool? expertMode = null,
        [Description("Format output for readability")]
        bool? formatOutput = null,
        [Description("Analyze all databases on the server")]
        bool? getAllDatabases = null,
        [Description("Include XML query plan columns in output (excluded by default to reduce response size)")]
        bool? includeQueryPlans = null,
        [Description("Include min/max/total metric columns (excluded by default to reduce response size)")]
        bool? verboseMetrics = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        if (top.HasValue)
            top = Math.Clamp(top.Value, 1, 100);

        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _darlingDataService.ExecuteQuickieStoreAsync(
                serverName, databaseName, sortOrder, top, startDate, endDate,
                executionCount, durationMs, procedureSchema, procedureName,
                includeQueryIds, includeQueryHashes, ignorePlanIds, ignoreQueryIds,
                queryTextSearch, queryTextSearchNot, waitFilter, queryType,
                expertMode, formatOutput, getAllDatabases,
                includeQueryPlans, verboseMetrics, verbose, cancellationToken), cancellationToken);
    }
}
