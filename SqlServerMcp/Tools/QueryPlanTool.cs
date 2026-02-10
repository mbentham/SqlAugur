using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class QueryPlanTool
{
    private readonly ISqlServerService _sqlServerService;

    public QueryPlanTool(ISqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    [McpServerTool(
        Name = "get_query_plan",
        Title = "Get Query Execution Plan",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Get the estimated or actual XML execution plan for a SELECT query. " +
        "Estimated plans show the optimizer's plan without executing. " +
        "Actual plans execute the query and include runtime statistics. " +
        "Uses the same query validation as read_data (SELECT only).")]
    public async Task<string> GetQueryPlan(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        [Description("SQL SELECT query to get the execution plan for. Only SELECT and WITH (CTE) queries are permitted.")] string query,
        [Description("Name of the database to query (use list_databases to see available databases)")] string databaseName,
        [Description("Plan type: 'estimated' (default, does not execute) or 'actual' (executes the query)")]
        string planType = "estimated",
        CancellationToken cancellationToken = default)
    {
        if (!planType.Equals("estimated", StringComparison.OrdinalIgnoreCase) &&
            !planType.Equals("actual", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException($"Invalid planType '{planType}'. Must be 'estimated' or 'actual'.");
        }

        return await ToolHelper.ExecuteAsync(() =>
            planType.Equals("actual", StringComparison.OrdinalIgnoreCase)
                ? _sqlServerService.GetActualPlanAsync(serverName, databaseName, query, cancellationToken)
                : _sqlServerService.GetEstimatedPlanAsync(serverName, databaseName, query, cancellationToken));
    }
}
