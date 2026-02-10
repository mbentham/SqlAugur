using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class QueryReproBuilderTool
{
    private readonly IDarlingDataService _darlingDataService;

    public QueryReproBuilderTool(IDarlingDataService darlingDataService)
    {
        _darlingDataService = darlingDataService;
    }

    [McpServerTool(
        Name = "sp_query_repro_builder",
        Title = "Query Reproduction Script Builder (sp_QueryReproBuilder)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_QueryReproBuilder to generate reproduction scripts for Query Store queries. Creates executable scripts with parameter values to reproduce query performance issues. Requires the DarlingData toolkit.")]
    public async Task<string> QueryReproBuilder(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Database containing Query Store data")]
        string? databaseName = null,
        [Description("Start date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? startDate = null,
        [Description("End date for analysis window (yyyy-MM-dd or yyyy-MM-dd HH:mm:ss)")]
        DateTime? endDate = null,
        [Description("Comma-separated list of plan IDs to include")]
        string? includePlanIds = null,
        [Description("Comma-separated list of query IDs to include")]
        string? includeQueryIds = null,
        [Description("Comma-separated list of plan IDs to ignore")]
        string? ignorePlanIds = null,
        [Description("Comma-separated list of query IDs to ignore")]
        string? ignoreQueryIds = null,
        [Description("Filter to a specific procedure schema")]
        string? procedureSchema = null,
        [Description("Filter to a specific procedure name")]
        string? procedureName = null,
        [Description("Search for queries containing this text")]
        string? queryTextSearch = null,
        [Description("Exclude queries containing this text")]
        string? queryTextSearchNot = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _darlingDataService.ExecuteQueryReproBuilderAsync(
                serverName, databaseName, startDate, endDate,
                includePlanIds, includeQueryIds, ignorePlanIds, ignoreQueryIds,
                procedureSchema, procedureName, queryTextSearch, queryTextSearchNot,
                cancellationToken));
    }
}
