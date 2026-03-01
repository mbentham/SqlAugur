using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

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
        IOptions<SqlAugurOptions> options,
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
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@what_to_check", whatToCheck);
        AddBoolParam(parameters, "@skip_queries", skipQueries);

        // Default: skip plan XML unless explicitly requested
        if (includeQueryPlans == true)
            AddBoolParam(parameters, "@skip_plan_xml", skipPlanXml ?? false);
        else if (!skipPlanXml.HasValue)
            parameters["@skip_plan_xml"] = 1;
        else
            AddBoolParam(parameters, "@skip_plan_xml", skipPlanXml);

        AddIfNotNull(parameters, "@minimum_disk_latency_ms", minimumDiskLatencyMs);
        AddIfNotNull(parameters, "@cpu_utilization_threshold", cpuUtilizationThreshold);
        AddBoolParam(parameters, "@skip_waits", skipWaits);
        AddBoolParam(parameters, "@skip_perfmon", skipPerfmon);
        AddIfNotNull(parameters, "@sample_seconds", sampleSeconds);
        AddBoolParam(parameters, "@troubleshoot_blocking", troubleshootBlocking);
        AddBoolParam(parameters, "@gimme_danger", gimmeDanger);

        var formatOptions = BuildPressureDetectorOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_PressureDetector", parameters, formatOptions, cancellationToken);
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
        bool? includeQueryPlans,
        bool? verboseMetrics,
        bool? verbose,
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
        AddBoolParam(parameters, "@format_output", formatOutput ?? false);
        AddBoolParam(parameters, "@get_all_databases", getAllDatabases);
        parameters["@hide_help_table"] = 1;

        var formatOptions = BuildQuickieStoreOptions(includeQueryPlans, verboseMetrics, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_QuickieStore", parameters, formatOptions, cancellationToken);
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
        bool? includeQueryPlans,
        bool? includeXmlReports,
        bool? verbose,
        int? maxRows,
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

        var formatOptions = BuildHealthParserOptions(includeQueryPlans, includeXmlReports, verbose, maxRows);
        return await ExecuteProcedureAsync(serverName, "sp_HealthParser", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteLogHunterAsync(
        string serverName,
        int? daysBack,
        DateTime? startDate,
        DateTime? endDate,
        string? customMessage,
        bool? customMessageOnly,
        bool? firstLogOnly,
        bool? verbose,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@days_back", daysBack ?? -3);
        AddIfNotNull(parameters, "@start_date", startDate);
        AddIfNotNull(parameters, "@end_date", endDate);
        AddIfNotNull(parameters, "@custom_message", customMessage);
        AddBoolParam(parameters, "@custom_message_only", customMessageOnly);
        AddBoolParam(parameters, "@first_log_only", firstLogOnly ?? true);

        var formatOptions = BuildLogHunterOptions(verbose, maxRows);
        return await ExecuteProcedureAsync(serverName, "sp_LogHunter", parameters, formatOptions, cancellationToken);
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
        bool? includeQueryPlans,
        bool? includeXmlReports,
        bool? verbose,
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

        var formatOptions = BuildHumanEventsBlockViewerOptions(includeQueryPlans, includeXmlReports, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_HumanEventsBlockViewer", parameters, formatOptions, cancellationToken);
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
        bool? verbose,
        int? maxRows,
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

        var formatOptions = BuildIndexCleanupOptions(verbose, maxRows);
        return await ExecuteProcedureAsync(serverName, "sp_IndexCleanup", parameters, formatOptions, cancellationToken);
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
        bool? includeQueryPlans,
        bool? verbose,
        int? maxRows,
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

        var formatOptions = BuildQueryReproBuilderOptions(includeQueryPlans, verbose, maxRows);
        return await ExecuteProcedureAsync(serverName, "sp_QueryReproBuilder", parameters, formatOptions, cancellationToken);
    }

    // ───────────────────────────────────────────────
    // Format option factories (internal static for testability)
    // ───────────────────────────────────────────────

    internal static ResultSetFormatOptions BuildPressureDetectorOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "query_plan", "live_query_plan" };

        if (includeQueryPlans == true)
        {
            excluded.Remove("query_plan");
            excluded.Remove("live_query_plan");
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["sql_text"] = 1000,
                ["tempdb_info"] = 2000
            }
        };
    }

    internal static ResultSetFormatOptions BuildQuickieStoreOptions(bool? includeQueryPlans, bool? verboseMetrics, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "query_plan" };

        if (includeQueryPlans == true)
            excluded.Remove("query_plan");

        if (verboseMetrics != true)
        {
            // Exclude known min/max/total metric columns
            foreach (var prefix in new[] { "min_", "max_", "total_" })
            {
                foreach (var metric in new[]
                {
                    "grant_kb", "used_grant_kb", "ideal_grant_kb",
                    "reserved_threads", "used_threads",
                    "columnstore_segment_reads", "columnstore_segment_skips",
                    "spills", "grant_mb", "used_grant_mb",
                    "min_columnstore_segment_reads", "min_columnstore_segment_skips",
                    "min_spills", "min_grant_mb", "min_used_grant_mb"
                })
                {
                    excluded.Add(prefix + metric);
                }

                foreach (var metric in new[]
                {
                    "duration_ms", "cpu_time_ms", "logical_io_reads",
                    "logical_io_writes", "physical_io_reads", "clr_time_ms",
                    "query_used_memory", "rowcount", "log_bytes_used",
                    "tempdb_space_used"
                })
                {
                    excluded.Add(prefix + metric);
                }
            }
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query_sql_text"] = 1000
            }
        };
    }

    internal static ResultSetFormatOptions BuildHealthParserOptions(bool? includeQueryPlans, bool? includeXmlReports, bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "deadlock_graph", "xml_deadlock_report", "blocked_process_report" };

        if (includeXmlReports == true)
        {
            excluded.Remove("deadlock_graph");
            excluded.Remove("xml_deadlock_report");
            excluded.Remove("blocked_process_report");
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query_text"] = 1000
            },
            MaxRowsOverride = maxRows
        };
    }

    internal static ResultSetFormatOptions BuildLogHunterOptions(bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

        return new ResultSetFormatOptions
        {
            MaxRowsOverride = maxRows ?? 200,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = 500
            }
        };
    }

    internal static ResultSetFormatOptions BuildHumanEventsBlockViewerOptions(bool? includeQueryPlans, bool? includeXmlReports, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "query_plan", "blocked_process_report_xml",
            "sql_handle", "statement_start_offset", "statement_end_offset"
        };

        if (includeQueryPlans == true)
            excluded.Remove("query_plan");

        if (includeXmlReports == true)
            excluded.Remove("blocked_process_report_xml");

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query_text"] = 1000
            }
        };
    }

    internal static ResultSetFormatOptions BuildIndexCleanupOptions(bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

        return new ResultSetFormatOptions
        {
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["original_index_definition"] = 500
            },
            MaxRowsOverride = maxRows
        };
    }

    internal static ResultSetFormatOptions BuildQueryReproBuilderOptions(bool? includeQueryPlans, bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "query_plan" };

        if (includeQueryPlans == true)
            excluded.Remove("query_plan");

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query_sql_text"] = 1000
            },
            MaxRowsOverride = maxRows
        };
    }
}
