using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class BlitzPlanCompareTool
{
    private readonly IFirstResponderService _frkService;
    private readonly IRateLimitingService _rateLimiter;

    public BlitzPlanCompareTool(IFirstResponderService frkService, IRateLimitingService rateLimiter)
    {
        _frkService = frkService;
        _rateLimiter = rateLimiter;
    }

    [McpServerTool(
        Name = "sp_blitz_plan_compare",
        Title = "Cross-Server Plan Comparison (sp_BlitzPlanCompare)",
        ReadOnly = true,
        Idempotent = true)]
    [Description(
        "Captures a query plan snapshot on one SQL Server and compares it to the cached plan on a second SQL Server, " +
        "without using linked servers. SqlAugur orchestrates both calls to sp_BlitzPlanCompare: capture on the first " +
        "server, compare on the second. In the result columns, the capture server appears as RemoteValue and the " +
        "compare server appears as LocalValue. Requires the First Responder Kit (demon_hunters branch) installed on both servers.")]
    public async Task<string> BlitzPlanCompare(
        [Description("Name of the SQL Server to capture the plan snapshot from (becomes RemoteValue in the output)")]
        string captureServerName,
        [Description("Name of the SQL Server to compare against (becomes LocalValue in the output)")]
        string compareServerName,
        [Description("BINARY(8) hex string (with or without 0x prefix) identifying a single cached plan — most specific")]
        string? queryPlanHash = null,
        [Description("BINARY(8) hex string (with or without 0x prefix); stable across servers, narrows to one-or-few plans")]
        string? queryHash = null,
        [Description("Stored procedure name ('proc', 'schema.proc', or 'db.schema.proc')")]
        string? storedProcName = null,
        [Description("Database name — required alongside storedProcName on Azure SQL DB")]
        string? databaseName = null,
        [Description("Include the reserved CallStack XML column in the compare result (excluded by default)")]
        bool? includeQueryPlans = null,
        [Description("Return all columns and full-length values with no truncation")]
        bool? verbose = null,
        CancellationToken cancellationToken = default)
    {
        var planHashBytes = ParseBinary8Hex(queryPlanHash, nameof(queryPlanHash));
        var queryHashBytes = ParseBinary8Hex(queryHash, nameof(queryHash));

        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _frkService.ExecuteBlitzPlanCompareAsync(
                captureServerName, compareServerName,
                planHashBytes, queryHashBytes,
                storedProcName, databaseName,
                includeQueryPlans, verbose,
                cancellationToken), cancellationToken);
    }

    private static byte[]? ParseBinary8Hex(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var hex = value.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (hex.Length != 16)
            throw new McpException(
                $"{parameterName}: expected a BINARY(8) hex string of 8 bytes (16 hex characters), got {hex.Length}.");

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException ex)
        {
            throw new McpException(
                $"{parameterName}: not a valid hex string ({ex.Message}).");
        }
    }
}
