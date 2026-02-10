using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class PressureDetectorTool
{
    private readonly IDarlingDataService _darlingDataService;

    public PressureDetectorTool(IDarlingDataService darlingDataService)
    {
        _darlingDataService = darlingDataService;
    }

    [McpServerTool(
        Name = "sp_pressure_detector",
        Title = "CPU & Memory Pressure Detector (sp_PressureDetector)",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Run sp_PressureDetector to diagnose CPU and memory pressure on SQL Server. Samples DMVs to identify resource bottlenecks, high-CPU queries, memory grants, and disk latency. Requires the DarlingData toolkit.")]
    public async Task<string> PressureDetector(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")]
        string serverName,
        [Description("What to check: 'all' (default), 'cpu', or 'memory'")]
        string? whatToCheck = null,
        [Description("Skip returning query-level details")]
        bool? skipQueries = null,
        [Description("Skip returning query plan XML")]
        bool? skipPlanXml = null,
        [Description("Minimum disk latency in ms to flag (default 100)")]
        int? minimumDiskLatencyMs = null,
        [Description("CPU utilization percentage threshold to flag (default 50)")]
        int? cpuUtilizationThreshold = null,
        [Description("Skip wait stats collection")]
        bool? skipWaits = null,
        [Description("Skip perfmon counter collection")]
        bool? skipPerfmon = null,
        [Description("Number of seconds to sample (0-10, default 0 for snapshot)")]
        int? sampleSeconds = null,
        [Description("Include blocking chain analysis")]
        bool? troubleshootBlocking = null,
        [Description("Enable tempdb pressure diagnostics (may add load on a stressed server)")]
        bool? gimmeDanger = null,
        CancellationToken cancellationToken = default)
    {
        if (sampleSeconds.HasValue)
            sampleSeconds = Math.Clamp(sampleSeconds.Value, 0, 10);

        return await ToolHelper.ExecuteAsync(() =>
            _darlingDataService.ExecutePressureDetectorAsync(
                serverName, whatToCheck, skipQueries, skipPlanXml,
                minimumDiskLatencyMs, cpuUtilizationThreshold, skipWaits,
                skipPerfmon, sampleSeconds, troubleshootBlocking, gimmeDanger,
                cancellationToken));
    }
}
