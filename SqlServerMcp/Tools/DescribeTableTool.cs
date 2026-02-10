using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class DescribeTableTool
{
    private readonly ITableDescribeService _tableDescribeService;

    public DescribeTableTool(ITableDescribeService tableDescribeService)
    {
        _tableDescribeService = tableDescribeService;
    }

    [McpServerTool(
        Name = "describe_table",
        Title = "Describe Table Structure",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Get comprehensive metadata about a single table including columns, data types, indexes, primary key, foreign keys, check constraints, and default constraints.")]
    public async Task<string> DescribeTable(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        [Description("Name of the database to query (use list_databases to see available databases)")] string databaseName,
        [Description("Name of the table to describe")] string tableName,
        [Description("Schema name (default 'dbo')")] string schemaName = "dbo",
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _tableDescribeService.DescribeTableAsync(
                serverName, databaseName, schemaName, tableName, cancellationToken));
    }
}
