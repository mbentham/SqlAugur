using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

public sealed class FirstResponderService : StoredProcedureServiceBase, IFirstResponderService
{
    internal static readonly HashSet<string> AllowedProcedures = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_Blitz", "sp_BlitzFirst", "sp_BlitzCache",
        "sp_BlitzIndex", "sp_BlitzWho", "sp_BlitzLock"
    };

    internal static readonly string[] BlockedParameters =
    [
        "@OutputDatabaseName", "@OutputSchemaName", "@OutputTableName",
        "@OutputServerName", "@OutputTableNameFileStats",
        "@OutputTableNamePerfmonStats", "@OutputTableNameWaitStats",
        "@OutputTableRetentionDays"
    ];

    public FirstResponderService(
        IOptions<SqlAugurOptions> options,
        ILogger<FirstResponderService> logger)
        : base(options, logger, AllowedProcedures, BlockedParameters,
            "output table parameters are blocked",
            "The First Responder Kit must be installed. " +
            "See: https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit")
    {
    }

    public async Task<string> ExecuteBlitzAsync(
        string serverName,
        bool? checkUserDatabaseObjects,
        bool? checkServerInfo,
        int? ignorePrioritiesAbove,
        bool? bringThePain,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddBoolParam(parameters, "@CheckUserDatabaseObjects", checkUserDatabaseObjects);
        AddBoolParam(parameters, "@CheckServerInfo", checkServerInfo);
        AddIfNotNull(parameters, "@IgnorePrioritiesAbove", ignorePrioritiesAbove);
        AddBoolParam(parameters, "@BringThePain", bringThePain);

        var formatOptions = BuildBlitzOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_Blitz", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzFirstAsync(
        string serverName,
        int? seconds,
        bool? expertMode,
        bool? showSleepingSpids,
        bool? sinceStartup,
        int? fileLatencyThresholdMs,
        bool? includeQueryPlans,
        bool? verbose,
        string? resultSets,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@Seconds", seconds);
        AddBoolParam(parameters, "@ExpertMode", expertMode);
        AddBoolParam(parameters, "@ShowSleepingSPIDs", showSleepingSpids);
        AddBoolParam(parameters, "@SinceStartup", sinceStartup);
        AddIfNotNull(parameters, "@FileLatencyThresholdMS", fileLatencyThresholdMs);
        AddIfNotNull(parameters, "@OutputResultSets", resultSets);

        var formatOptions = BuildBlitzFirstOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzFirst", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzCacheAsync(
        string serverName,
        string? sortOrder,
        int? top,
        bool? expertMode,
        string? databaseName,
        string? slowlySearchPlansFor,
        bool? exportToExcel,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@SortOrder", sortOrder);
        AddIfNotNull(parameters, "@Top", top ?? 10);
        AddBoolParam(parameters, "@ExpertMode", expertMode);
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        AddIfNotNull(parameters, "@SlowlySearchPlansFor", slowlySearchPlansFor);
        if (!parameters.ContainsKey("@ExportToExcel"))
            AddBoolParam(parameters, "@ExportToExcel", exportToExcel ?? true);

        var formatOptions = BuildBlitzCacheOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzCache", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzIndexAsync(
        string serverName,
        string? databaseName,
        string? schemaName,
        string? tableName,
        bool? getAllDatabases,
        int? mode,
        int? thresholdMb,
        int? filter,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        AddIfNotNull(parameters, "@SchemaName", schemaName);
        AddIfNotNull(parameters, "@TableName", tableName);
        AddBoolParam(parameters, "@GetAllDatabases", getAllDatabases);
        AddIfNotNull(parameters, "@Mode", mode);
        AddIfNotNull(parameters, "@ThresholdMB", thresholdMb);
        AddIfNotNull(parameters, "@Filter", filter);

        var formatOptions = BuildBlitzIndexOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzIndex", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzWhoAsync(
        string serverName,
        bool? expertMode,
        bool? showSleepingSpids,
        int? minElapsedSeconds,
        int? minCpuTime,
        int? minLogicalReads,
        int? minBlockingSeconds,
        int? minTempdbMb,
        bool? showActualParameters,
        bool? getLiveQueryPlan,
        string? sortOrder,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddBoolParam(parameters, "@ExpertMode", expertMode);
        AddBoolParam(parameters, "@ShowSleepingSPIDs", showSleepingSpids);
        AddIfNotNull(parameters, "@MinElapsedSeconds", minElapsedSeconds);
        AddIfNotNull(parameters, "@MinCPUTime", minCpuTime);
        AddIfNotNull(parameters, "@MinLogicalReads", minLogicalReads);
        AddIfNotNull(parameters, "@MinBlockingSeconds", minBlockingSeconds);
        AddIfNotNull(parameters, "@MinTempdbMB", minTempdbMb);
        AddBoolParam(parameters, "@ShowActualParameters", showActualParameters);
        AddBoolParam(parameters, "@GetLiveQueryPlan", getLiveQueryPlan);
        AddIfNotNull(parameters, "@SortOrder", sortOrder);

        var formatOptions = BuildBlitzWhoOptions(includeQueryPlans, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzWho", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzLockAsync(
        string serverName,
        string? databaseName,
        DateTime? startDate,
        DateTime? endDate,
        string? objectName,
        string? storedProcName,
        string? appName,
        string? hostName,
        string? loginName,
        bool? victimsOnly,
        string? eventSessionName,
        bool? includeQueryPlans,
        bool? includeXmlReports,
        bool? verbose,
        int? daysBack,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        AddIfNotNull(parameters, "@StartDate", startDate ?? DateTime.UtcNow.AddDays(-(daysBack ?? 1)));
        AddIfNotNull(parameters, "@EndDate", endDate);
        AddIfNotNull(parameters, "@ObjectName", objectName);
        AddIfNotNull(parameters, "@StoredProcName", storedProcName);
        AddIfNotNull(parameters, "@AppName", appName);
        AddIfNotNull(parameters, "@HostName", hostName);
        AddIfNotNull(parameters, "@LoginName", loginName);
        AddBoolParam(parameters, "@VictimsOnly", victimsOnly);
        AddIfNotNull(parameters, "@EventSessionName", eventSessionName);

        var formatOptions = BuildBlitzLockOptions(includeQueryPlans, includeXmlReports, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzLock", parameters, formatOptions, cancellationToken);
    }

    // ───────────────────────────────────────────────
    // Format option factories (internal static for testability)
    // ───────────────────────────────────────────────

    internal static ResultSetFormatOptions BuildBlitzOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "QueryPlan", "QueryPlanFiltered" };

        if (includeQueryPlans == true)
        {
            excluded.Remove("QueryPlan");
            excluded.Remove("QueryPlanFiltered");
            return new ResultSetFormatOptions
            {
                ExcludedColumns = excluded,
                TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Details"] = 2000
                }
            };
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Details"] = 2000
            }
        };
    }

    internal static ResultSetFormatOptions BuildBlitzFirstOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "QueryPlan", "PlanHandle", "QueryStatsNowID", "QueryStatsFirstID" };

        if (includeQueryPlans == true)
        {
            excluded.Remove("QueryPlan");
            return new ResultSetFormatOptions
            {
                ExcludedColumns = excluded,
                TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["QueryText"] = 500,
                    ["HowToStopIt"] = 1000
                }
            };
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["QueryText"] = 500,
                ["HowToStopIt"] = 1000
            }
        };
    }

    internal static ResultSetFormatOptions BuildBlitzCacheOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "QueryPlan", "Query Plan",
            "implicit_conversion_info", "Implicit Conversion Info",
            "cached_execution_parameters", "Cached Execution Parameters",
            "missing_indexes", "Missing Indexes",
            "PlanHandle", "Plan Handle",
            "SqlHandle", "SQL Handle",
            "QueryHash", "QueryPlanHash",
            "Remove Plan Handle From Cache",
            "SetOptions", "SET Options",
            "StatementStartOffset", "StatementEndOffset",
            "PlanGenerationNum",
            "ai_prompt", "ai_payload", "ai_raw_response"
        };

        if (includeQueryPlans == true)
        {
            excluded.Remove("QueryPlan");
            excluded.Remove("Query Plan");
            return new ResultSetFormatOptions
            {
                ExcludedColumns = excluded,
                TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["QueryText"] = 500,
                    ["Query Text"] = 500,
                    ["Warnings"] = 2000
                }
            };
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["QueryText"] = 500,
                ["Query Text"] = 500,
                ["Warnings"] = 2000
            }
        };
    }

    internal static ResultSetFormatOptions BuildBlitzIndexOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "sample_query_plan", "more_info", "blitz_result_id", "check_id", "index_sanity_id" };

        if (includeQueryPlans == true)
        {
            excluded.Remove("sample_query_plan");
            return new ResultSetFormatOptions
            {
                ExcludedColumns = excluded,
                TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["create_tsql"] = 1000,
                    ["details"] = 2000,
                    ["index_definition"] = 500,
                    ["secret_columns"] = 500
                }
            };
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["create_tsql"] = 1000,
                ["details"] = 2000,
                ["index_definition"] = 500,
                ["secret_columns"] = 500
            }
        };
    }

    internal static ResultSetFormatOptions BuildBlitzWhoOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "query_plan", "live_query_plan",
            "cached_parameter_info", "Live_Parameter_Info",
            "fix_parameter_sniffing", "context_info",
            "sql_handle", "plan_handle",
            "statement_start_offset", "statement_end_offset",
            "query_hash", "query_plan_hash",
            "outer_command"
        };

        if (includeQueryPlans == true)
        {
            excluded.Remove("query_plan");
            excluded.Remove("live_query_plan");
            return new ResultSetFormatOptions
            {
                ExcludedColumns = excluded,
                TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["query_text"] = 500,
                    ["top_session_waits"] = 500
                }
            };
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query_text"] = 500,
                ["top_session_waits"] = 500
            }
        };
    }

    internal static ResultSetFormatOptions BuildBlitzLockOptions(bool? includeQueryPlans, bool? includeXmlReports, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "deadlock_graph", "process_xml", "parallel_deadlock_details", "query_plan" };

        if (includeQueryPlans == true)
            excluded.Remove("query_plan");

        if (includeXmlReports == true)
        {
            excluded.Remove("deadlock_graph");
            excluded.Remove("process_xml");
            excluded.Remove("parallel_deadlock_details");
        }

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = 500,
                ["query_xml"] = 500,
                ["object_names"] = 500,
                ["finding"] = 2000
            }
        };
    }
}
