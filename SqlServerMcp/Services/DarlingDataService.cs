using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

public sealed class DarlingDataService : StoredProcedureServiceBase, IDarlingDataService
{
    internal static readonly HashSet<string> AllowedProcedures = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_PressureDetector", "sp_QuickieStore", "sp_HealthParser",
        "sp_LogHunter", "sp_HumanEventsBlockViewer", "sp_IndexCleanup",
        "sp_QueryReproBuilder"
    };

    internal static readonly string[] BlockedParameters =
    [
        "@log_to_table",
        "@log_database_name",
        "@log_schema_name",
        "@log_table_name_prefix",
        "@log_retention_days",
        "@output_database_name",
        "@output_schema_name",
        "@delete_retention_days"
    ];

    public DarlingDataService(
        IOptions<SqlServerMcpOptions> options,
        ILogger<DarlingDataService> logger)
        : base(options, logger, AllowedProcedures, BlockedParameters,
            "output/logging parameters are blocked",
            "The DarlingData toolkit must be installed. " +
            "See: https://github.com/erikdarlingdata/DarlingData")
    {
    }

    public async Task<string> ExecutePressureDetectorAsync(
        string serverName,
        string? whatToCheck,
        bool? skipQueries,
        bool? skipPlanXml,
        int? minimumDiskLatencyMs,
        int? cpuUtilizationThreshold,
        bool? skipWaits,
        bool? skipPerfmon,
        int? sampleSeconds,
        bool? troubleshootBlocking,
        bool? gimmeDanger,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@what_to_check", whatToCheck);
        AddBoolParam(parameters, "@skip_queries", skipQueries);
        AddBoolParam(parameters, "@skip_plan_xml", skipPlanXml);
        AddIfNotNull(parameters, "@minimum_disk_latency_ms", minimumDiskLatencyMs);
        AddIfNotNull(parameters, "@cpu_utilization_threshold", cpuUtilizationThreshold);
        AddBoolParam(parameters, "@skip_waits", skipWaits);
        AddBoolParam(parameters, "@skip_perfmon", skipPerfmon);
        AddIfNotNull(parameters, "@sample_seconds", sampleSeconds);
        AddBoolParam(parameters, "@troubleshoot_blocking", troubleshootBlocking);
        AddBoolParam(parameters, "@gimme_danger", gimmeDanger);

        return await ExecuteProcedureAsync(serverName, "sp_PressureDetector", parameters, cancellationToken);
    }

    public async Task<string> ExecuteQuickieStoreAsync(
        string serverName,
        string? databaseName,
        string? sortOrder,
        int? top,
        DateTime? startDate,
        DateTime? endDate,
        int? executionCount,
        int? durationMs,
        string? procedureSchema,
        string? procedureName,
        string? includeQueryIds,
        string? includeQueryHashes,
        string? ignorePlanIds,
        string? ignoreQueryIds,
        string? queryTextSearch,
        string? queryTextSearchNot,
        string? waitFilter,
        string? queryType,
        bool? expertMode,
        bool? formatOutput,
        bool? getAllDatabases,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@database_name", databaseName);
        AddIfNotNull(parameters, "@sort_order", sortOrder);
        AddIfNotNull(parameters, "@top", top);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddIfNotNull(parameters, "@execution_count", executionCount);
        AddIfNotNull(parameters, "@duration_ms", durationMs);
        AddIfNotNull(parameters, "@procedure_schema", procedureSchema);
        AddIfNotNull(parameters, "@procedure_name", procedureName);
        AddIfNotNull(parameters, "@include_query_ids", includeQueryIds);
        AddIfNotNull(parameters, "@include_query_hashes", includeQueryHashes);
        AddIfNotNull(parameters, "@ignore_plan_ids", ignorePlanIds);
        AddIfNotNull(parameters, "@ignore_query_ids", ignoreQueryIds);
        AddIfNotNull(parameters, "@query_text_search", queryTextSearch);
        AddIfNotNull(parameters, "@query_text_search_not", queryTextSearchNot);
        AddIfNotNull(parameters, "@wait_filter", waitFilter);
        AddIfNotNull(parameters, "@query_type", queryType);
        AddBoolParam(parameters, "@expert_mode", expertMode);
        AddBoolParam(parameters, "@format_output", formatOutput);
        AddBoolParam(parameters, "@get_all_databases", getAllDatabases);

        return await ExecuteProcedureAsync(serverName, "sp_QuickieStore", parameters, cancellationToken);
    }

    public async Task<string> ExecuteHealthParserAsync(
        string serverName,
        string? whatToCheck,
        DateTime? startDate,
        DateTime? endDate,
        bool? warningsOnly,
        string? databaseName,
        int? waitDurationMs,
        int? waitRoundIntervalMinutes,
        bool? skipLocks,
        int? pendingTaskThreshold,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@what_to_check", whatToCheck);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddBoolParam(parameters, "@warnings_only", warningsOnly);
        AddIfNotNull(parameters, "@database_name", databaseName);
        AddIfNotNull(parameters, "@wait_duration_ms", waitDurationMs);
        AddIfNotNull(parameters, "@wait_round_interval_minutes", waitRoundIntervalMinutes);
        AddBoolParam(parameters, "@skip_locks", skipLocks);
        AddIfNotNull(parameters, "@pending_task_threshold", pendingTaskThreshold);

        return await ExecuteProcedureAsync(serverName, "sp_HealthParser", parameters, cancellationToken);
    }

    public async Task<string> ExecuteLogHunterAsync(
        string serverName,
        int? daysBack,
        DateTime? startDate,
        DateTime? endDate,
        string? customMessage,
        bool? customMessageOnly,
        bool? firstLogOnly,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@days_back", daysBack);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddIfNotNull(parameters, "@custom_message", customMessage);
        AddBoolParam(parameters, "@custom_message_only", customMessageOnly);
        AddBoolParam(parameters, "@first_log_only", firstLogOnly);

        return await ExecuteProcedureAsync(serverName, "sp_LogHunter", parameters, cancellationToken);
    }

    public async Task<string> ExecuteHumanEventsBlockViewerAsync(
        string serverName,
        string? sessionName,
        string? targetType,
        DateTime? startDate,
        DateTime? endDate,
        string? databaseName,
        string? objectName,
        int? maxBlockingEvents,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@session_name", sessionName);
        AddIfNotNull(parameters, "@target_type", targetType);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddIfNotNull(parameters, "@database_name", databaseName);
        AddIfNotNull(parameters, "@object_name", objectName);
        AddIfNotNull(parameters, "@max_blocking_events", maxBlockingEvents);

        return await ExecuteProcedureAsync(serverName, "sp_HumanEventsBlockViewer", parameters, cancellationToken);
    }

    public async Task<string> ExecuteIndexCleanupAsync(
        string serverName,
        string? databaseName,
        string? schemaName,
        string? tableName,
        int? minReads,
        int? minWrites,
        int? minSizeGb,
        int? minRows,
        bool? dedupeOnly,
        bool? getAllDatabases,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@database_name", databaseName);
        AddIfNotNull(parameters, "@schema_name", schemaName);
        AddIfNotNull(parameters, "@table_name", tableName);
        AddIfNotNull(parameters, "@min_reads", minReads);
        AddIfNotNull(parameters, "@min_writes", minWrites);
        AddIfNotNull(parameters, "@min_size_gb", minSizeGb);
        AddIfNotNull(parameters, "@min_rows", minRows);
        AddBoolParam(parameters, "@dedupe_only", dedupeOnly);
        AddBoolParam(parameters, "@get_all_databases", getAllDatabases);

        return await ExecuteProcedureAsync(serverName, "sp_IndexCleanup", parameters, cancellationToken);
    }

    public async Task<string> ExecuteQueryReproBuilderAsync(
        string serverName,
        string? databaseName,
        DateTime? startDate,
        DateTime? endDate,
        string? includePlanIds,
        string? includeQueryIds,
        string? ignorePlanIds,
        string? ignoreQueryIds,
        string? procedureSchema,
        string? procedureName,
        string? queryTextSearch,
        string? queryTextSearchNot,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@database_name", databaseName);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddIfNotNull(parameters, "@include_plan_ids", includePlanIds);
        AddIfNotNull(parameters, "@include_query_ids", includeQueryIds);
        AddIfNotNull(parameters, "@ignore_plan_ids", ignorePlanIds);
        AddIfNotNull(parameters, "@ignore_query_ids", ignoreQueryIds);
        AddIfNotNull(parameters, "@procedure_schema", procedureSchema);
        AddIfNotNull(parameters, "@procedure_name", procedureName);
        AddIfNotNull(parameters, "@query_text_search", queryTextSearch);
        AddIfNotNull(parameters, "@query_text_search_not", queryTextSearchNot);

        return await ExecuteProcedureAsync(serverName, "sp_QueryReproBuilder", parameters, cancellationToken);
    }
}
