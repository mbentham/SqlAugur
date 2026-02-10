using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class ReadDataTool
{
    private readonly ISqlServerService _sqlServerService;

    public ReadDataTool(ISqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    [McpServerTool(
        Name = "read_data",
        Title = "Read Data from SQL Server",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Execute a read-only SQL SELECT query against a named SQL Server instance. Only SELECT and WITH (CTE) queries are allowed. Use list_servers first to discover available server names.")]
    public async Task<string> ReadData(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        [Description("SQL SELECT query to execute. Only SELECT and WITH (CTE) queries are permitted.")] string query,
        [Description("Name of the database to query (use list_databases to see available databases)")] string databaseName,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _sqlServerService.ExecuteQueryAsync(serverName, databaseName, query, cancellationToken));
    }
}
