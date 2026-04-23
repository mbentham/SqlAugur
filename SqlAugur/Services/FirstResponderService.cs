using System.Data;
using System.Data.SqlTypes;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

public sealed class FirstResponderService : StoredProcedureServiceBase, IFirstResponderService
{
    internal static readonly HashSet<string> AllowedProcedures = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_Blitz", "sp_BlitzFirst", "sp_BlitzCache",
        "sp_BlitzIndex", "sp_BlitzWho", "sp_BlitzLock",
        "sp_BlitzPlanCompare"
    };

    internal static readonly string[] BlockedParameters =
    [
        "@OutputDatabaseName", "@OutputSchemaName", "@OutputTableName",
        "@OutputServerName", "@OutputTableNameFileStats",
        "@OutputTableNamePerfmonStats", "@OutputTableNameWaitStats",
        "@OutputTableRetentionDays",
        "@LinkedServer"
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
        int? maxRows,
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

        var formatOptions = BuildBlitzIndexOptions(includeQueryPlans, verbose, maxRows);
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
        int? maxRows,
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

        var formatOptions = BuildBlitzLockOptions(includeQueryPlans, includeXmlReports, verbose, maxRows);
        return await ExecuteProcedureAsync(serverName, "sp_BlitzLock", parameters, formatOptions, cancellationToken);
    }

    public async Task<string> ExecuteBlitzPlanCompareAsync(
        string captureServerName,
        string compareServerName,
        byte[]? queryPlanHash,
        byte[]? queryHash,
        string? storedProcName,
        string? databaseName,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var captureParams = BuildCaptureParameters(
            queryPlanHash, queryHash, storedProcName, databaseName);

        var callStack = await CaptureSnapshotAsync(
            captureServerName, captureParams, cancellationToken);

        var snapshotXml = ExtractSnapshotXml(callStack);

        // Reader consumed lazily by SqlClient during ExecuteReaderAsync; do not dispose here.
        var compareParams = new Dictionary<string, object?>
        {
            ["@CompareToXML"] = new SqlXml(XmlReader.Create(new StringReader(snapshotXml)))
        };

        var formatOptions = BuildBlitzPlanCompareOptions(includeQueryPlans, verbose);

        return await ExecuteProcedureAsync(
            compareServerName, "sp_BlitzPlanCompare", compareParams,
            formatOptions, cancellationToken);
    }

    internal static Dictionary<string, object?> BuildCaptureParameters(
        byte[]? queryPlanHash,
        byte[]? queryHash,
        string? storedProcName,
        string? databaseName)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@QueryPlanHash", queryPlanHash);
        AddIfNotNull(parameters, "@QueryHash", queryHash);
        AddIfNotNull(parameters, "@StoredProcName", storedProcName);
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        return parameters;
    }

    private async Task<string> CaptureSnapshotAsync(
        string serverName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        foreach (var paramName in parameters.Keys)
        {
            if (BlockedParameters.Any(blocked =>
                paramName.Equals(blocked, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Parameter '{paramName}' is not allowed (output table parameters are blocked).");
            }
        }

        var serverConfig = Options.ResolveServer(serverName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("sp_BlitzPlanCompare", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = Options.CommandTimeoutSeconds
        };

        foreach (var (name, value) in parameters)
        {
            if (value is not null)
                command.Parameters.AddWithValue(name, value);
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"sp_BlitzPlanCompare returned no rows on server '{serverName}' — " +
                    $"no matching plan found for the supplied identifiers.");
            }
            if (reader.IsDBNull(0))
            {
                throw new InvalidOperationException(
                    $"sp_BlitzPlanCompare returned a null CallStack on server '{serverName}'.");
            }
            return reader.GetString(0);
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            throw new InvalidOperationException(
                $"Stored procedure 'sp_BlitzPlanCompare' not found on server '{serverName}'. " +
                "Requires the First Responder Kit demon_hunters branch: " +
                "https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit/tree/demon_hunters");
        }
    }

    // ───────────────────────────────────────────────
    // Capture-phase snapshot XML extraction
    // ───────────────────────────────────────────────

    private const string SnapshotRootElement = "BlitzPlanCompareSnapshot";

    internal static string ExtractSnapshotXml(string callStack)
    {
        var openMarker = "<" + SnapshotRootElement;
        var closeMarker = "</" + SnapshotRootElement + ">";

        var start = callStack.IndexOf(openMarker, StringComparison.Ordinal);
        var endTag = callStack.LastIndexOf(closeMarker, StringComparison.Ordinal);

        if (start < 0 || endTag < 0 || endTag < start)
        {
            throw new InvalidOperationException(
                $"sp_BlitzPlanCompare did not return a recognizable snapshot envelope " +
                $"(expected <{SnapshotRootElement}> root). Upstream proc may have changed.");
        }

        var end = endTag + closeMarker.Length;
        var sliced = callStack[start..end];

        // Un-double T-SQL single-quote escapes ('' → ')
        var undoubled = sliced.Replace("''", "'");

        try
        {
            _ = XDocument.Parse(undoubled);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException(
                $"extracted snapshot XML failed to parse: {ex.Message}", ex);
        }

        return undoubled;
    }

    // ───────────────────────────────────────────────
    // Format option factories (internal static for testability)
    // ───────────────────────────────────────────────

    internal static ResultSetFormatOptions BuildBlitzOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includeQueryPlans != true)
        {
            excluded.Add("QueryPlan");
            excluded.Add("QueryPlanFiltered");
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

    internal static ResultSetFormatOptions BuildBlitzIndexOptions(bool? includeQueryPlans, bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

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
                },
                MaxRowsOverride = maxRows
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
            },
            MaxRowsOverride = maxRows
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

    internal static ResultSetFormatOptions BuildBlitzLockOptions(bool? includeQueryPlans, bool? includeXmlReports, bool? verbose, int? maxRows = null)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue, MaxRowsOverride = maxRows };

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
            },
            MaxRowsOverride = maxRows
        };
    }

    internal static ResultSetFormatOptions BuildBlitzPlanCompareOptions(bool? includeQueryPlans, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (includeQueryPlans != true)
            excluded.Add("CallStack");

        return new ResultSetFormatOptions
        {
            ExcludedColumns = excluded,
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Finding"] = 2000,
                ["Details"] = 2000,
                ["URL"] = 500
            }
        };
    }
}
