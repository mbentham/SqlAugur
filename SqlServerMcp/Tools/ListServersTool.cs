using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class ListServersTool
{
    private readonly ISqlServerService _sqlServerService;

    public ListServersTool(ISqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    [McpServerTool(
        Name = "list_servers",
        Title = "List SQL Servers",
        ReadOnly = true,
        Idempotent = true)]
    [Description("List the available SQL Server instances that can be queried. Call this first to discover server names before using read_data.")]
    public string ListServers()
    {
        var servers = _sqlServerService.GetServerNames();
        return string.Join(", ", servers);
    }
}
