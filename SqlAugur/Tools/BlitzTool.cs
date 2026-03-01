using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class BlitzTool
{
    private readonly IFirstResponderService _frkService;
    private readonly IRateLimitingService _rateLimiter;

    public BlitzTool(IFirstResponderService frkService, IRateLimitingService rateLimiter)
    {
        _frkService = frkService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_blitz",
        Title = "Server Health Check (sp_Blitz)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_Blitz to perform an overall SQL Server health check. Returns prioritized findings including performance, configuration, and security issues. Requires the First Responder Kit to be installed on the target server.")]
    public async Task<string> Blitz(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("Check user database objects for issues (default true). Set to false for faster results.")]
        bool? checkUserDatabaseObjects = null,
        [Description("Include server-level configuration info in results (default false)")]
        bool? checkServerInfo = null,
        [Description("Only show findings with priority at or below this number (e.g. 50 for critical only, 255 for all)")]
        int? ignorePrioritiesAbove = null,
        [Description("Required for some intensive checks. Set to true to enable deep analysis.")]
        bool? bringThePain = null,
        [Description("Include XML query plan columns in output (excluded by default to reduce response size)")]
        bool? includeQueryPlans = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _frkService.ExecuteBlitzAsync(
                serverName, checkUserDatabaseObjects, checkServerInfo,
                ignorePrioritiesAbove, bringThePain,
                includeQueryPlans, verbose, cancellationToken), cancellationToken);
    }
}
