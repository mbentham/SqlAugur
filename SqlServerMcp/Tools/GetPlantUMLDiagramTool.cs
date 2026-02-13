using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class GetPlantUMLDiagramTool
{
    private readonly IDiagramService _diagramService;
    private readonly IRateLimitingService _rateLimiter;

    public GetPlantUMLDiagramTool(IDiagramService diagramService, IRateLimitingService rateLimiter)
    {
        _diagramService = diagramService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "get_plantuml_diagram",
        Title = "Get Database ER Diagram",
        ReadOnly = false,
        Idempotent = false)]
    [Description("Generate a PlantUML ER diagram saved to a file. Shows tables, columns, PKs, and FK relationships with smart cardinality.")]
    public async Task<string> GetDiagram(
        [Description("Server name from list_servers")] string serverName,
        [Description("Database name from list_databases")] string databaseName,
        [Description("File path for output (e.g. '/tmp/diagram.puml')")] string outputPath,
        [Description("Optional comma-separated schemas to include (e.g. 'dbo,sales'). Overrides excludeSchemas.")] string? includeSchemas = null,
        [Description("Optional comma-separated schemas to exclude (e.g. 'audit,staging'). Ignored if includeSchemas set.")] string? excludeSchemas = null,
        [Description("Optional comma-separated tables to include (e.g. 'Users,Orders'). Overrides excludeTables.")] string? includeTables = null,
        [Description("Optional comma-separated tables to exclude. Ignored if includeTables set.")] string? excludeTables = null,
        [Description("Max tables to include (1-200, default 50)")] int maxTables = 50,
        [Description("true/false. Show only PK/FK columns without data types")] bool compact = false,
        CancellationToken cancellationToken = default)
    {
        maxTables = Math.Clamp(maxTables, 1, 200);

        var puml = await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _diagramService.GenerateDiagramAsync(
                serverName, databaseName,
                ToolHelper.ParseCommaSeparatedList(includeSchemas), ToolHelper.ParseCommaSeparatedList(excludeSchemas),
                ToolHelper.ParseCommaSeparatedList(includeTables), ToolHelper.ParseCommaSeparatedList(excludeTables),
                maxTables, cancellationToken, compact), cancellationToken);

        return await ToolHelper.SaveToFileAsync(puml, outputPath, ".puml", "PlantUML diagram", cancellationToken);
    }
}
