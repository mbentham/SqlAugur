using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class BlitzIndexTool
{
    private readonly IFirstResponderService _frkService;
    private readonly IRateLimitingService _rateLimiter;

    public BlitzIndexTool(IFirstResponderService frkService, IRateLimitingService rateLimiter)
    {
        _frkService = frkService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_blitz_index",
        Title = "Index Analysis (sp_BlitzIndex)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_BlitzIndex for index analysis and tuning recommendations. Identifies missing indexes, unused indexes, duplicate indexes, and index usage patterns. Requires the First Responder Kit.")]
    public async Task<string> BlitzIndex(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Database to analyze (required for most modes)")]
        string? databaseName = null,
        [Description("Schema name to filter to (e.g. 'dbo')")]
        string? schemaName = null,
        [Description("Table name to analyze in detail")]
        string? tableName = null,
        [Description("Analyze all databases (can be slow on large servers)")]
        bool? getAllDatabases = null,
        [Description("Analysis mode: 0=Diagnose (default), 1=Summarize, 2=Index Usage Detail, 3=Missing Indexes, 4=Diagnose Details")]
        int? mode = null,
        [Description("Minimum table size in MB to include in analysis")]
        int? thresholdMb = null,
        [Description("Filter: 0=All (default), 1=No warnings for 0-read objects, 2=No read indexes, 3=High maintenance, 4=Active tables only")]
        int? filter = null,
        [Description("Include sample query plan columns in output (excluded by default to reduce response size)")]
        bool? includeQueryPlans = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _frkService.ExecuteBlitzIndexAsync(
                serverName, databaseName, schemaName, tableName,
                getAllDatabases, mode, thresholdMb, filter,
                includeQueryPlans, verbose, cancellationToken), cancellationToken);
    }
}
