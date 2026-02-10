using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class IndexCleanupTool
{
    private readonly IDarlingDataService _darlingDataService;

    public IndexCleanupTool(IDarlingDataService darlingDataService)
    {
        _darlingDataService = darlingDataService;
    }

    [McpServerTool(
        Name = "sp_index_cleanup",
        Title = "Unused Index Finder (sp_IndexCleanup)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_IndexCleanup to find unused and duplicate indexes that are candidates for removal. Analyzes index usage statistics to identify indexes with low reads and high write overhead. Requires the DarlingData toolkit.")]
    public async Task<string> IndexCleanup(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Database to analyze")]
        string? databaseName = null,
        [Description("Schema name to filter to (e.g. 'dbo')")]
        string? schemaName = null,
        [Description("Table name to analyze in detail")]
        string? tableName = null,
        [Description("Minimum user reads threshold to consider an index 'used'")]
        int? minReads = null,
        [Description("Minimum user writes to include in results")]
        int? minWrites = null,
        [Description("Minimum index size in GB to include")]
        int? minSizeGb = null,
        [Description("Minimum row count to include")]
        int? minRows = null,
        [Description("Only show duplicate indexes")]
        bool? dedupeOnly = null,
        [Description("Analyze all databases on the server")]
        bool? getAllDatabases = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _darlingDataService.ExecuteIndexCleanupAsync(
                serverName, databaseName, schemaName, tableName,
                minReads, minWrites, minSizeGb, minRows,
                dedupeOnly, getAllDatabases, cancellationToken));
    }
}
