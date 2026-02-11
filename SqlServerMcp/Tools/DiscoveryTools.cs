using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class DiscoveryTools
{
    private readonly IToolsetManager _toolsetManager;

    public DiscoveryTools(IToolsetManager toolsetManager)
    {
        _toolsetManager = toolsetManager;
    }

    [McpServerTool(
        Name = "list_toolsets",
        Title = "List DBA Toolsets",
        ReadOnly = true,
        Idempotent = true)]
    [Description("List available DBA diagnostic toolsets and their current status. Use this to discover what additional tools can be enabled for database performance analysis.")]
    public string ListToolsets()
    {
        return _toolsetManager.GetToolsetSummaries();
    }

    [McpServerTool(
        Name = "get_toolset_tools",
        Title = "Get Toolset Details",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Get detailed information about a specific toolset's tools and parameters before enabling it.")]
    public string GetToolsetTools(
        [Description("Toolset name: 'first_responder_kit', 'darling_data', or 'whoisactive'")]
        string toolsetName)
    {
        return _toolsetManager.GetToolsetDetails(toolsetName);
    }

    [McpServerTool(
        Name = "enable_toolset",
        Title = "Enable DBA Toolset",
        ReadOnly = true)]
    [Description("Enable a DBA diagnostic toolset, making its tools available for use. The toolset must be configured in server settings.")]
    public string EnableToolset(
        [Description("Toolset name: 'first_responder_kit', 'darling_data', or 'whoisactive'")]
        string toolsetName)
    {
        return _toolsetManager.EnableToolset(toolsetName);
    }
}
