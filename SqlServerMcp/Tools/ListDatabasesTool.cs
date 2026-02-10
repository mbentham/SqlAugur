using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class ListDatabasesTool
{
    private readonly ISqlServerService _sqlServerService;

    public ListDatabasesTool(ISqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    [McpServerTool(
        Name = "list_databases",
        Title = "List Databases on SQL Server",
        ReadOnly = true,
        Idempotent = true)]
    [Description("List all databases on a named SQL Server instance. Returns database names, IDs, states, and creation dates. Use list_servers first to discover available server names.")]
    public async Task<string> ListDatabases(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(() =>
            _sqlServerService.ListDatabasesAsync(serverName, cancellationToken));
    }
}
