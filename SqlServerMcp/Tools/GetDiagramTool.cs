using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class GetDiagramTool
{
    private readonly IDiagramService _diagramService;

    public GetDiagramTool(IDiagramService diagramService)
    {
        _diagramService = diagramService;
    }

    [McpServerTool(
        Name = "get_diagram",
        Title = "Get Database ER Diagram",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Generate a PlantUML ER diagram for a SQL Server database. Returns PlantUML text showing tables, columns, primary keys, and foreign key relationships with smart cardinality.")]
    public async Task<string> GetDiagram(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        [Description("Name of the database to diagram (use list_databases to see available databases)")] string databaseName,
        [Description("Optional schema name to filter tables (e.g. 'dbo'). If omitted, all user schemas are included.")] string? schemaFilter = null,
        [Description("Maximum number of tables to include (1-200, default 50)")] int maxTables = 50,
        CancellationToken cancellationToken = default)
    {
        maxTables = Math.Clamp(maxTables, 1, 200);

        return await ToolHelper.ExecuteAsync(() =>
            _diagramService.GenerateDiagramAsync(
                serverName, databaseName, schemaFilter, maxTables, cancellationToken));
    }
}
