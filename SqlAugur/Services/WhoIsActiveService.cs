using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

public sealed class WhoIsActiveService : StoredProcedureServiceBase, IWhoIsActiveService
{
    internal static readonly HashSet<string> AllowedProcedures = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_WhoIsActive"
    };

    internal static readonly string[] BlockedParameters =
    [
        "@destination_table",
        "@return_schema",
        "@schema",
        "@help"
    ];

    internal const string DefaultOutputColumnList =
        "[session_id][status][wait_info][blocking_session_id][blocked_session_count]" +
        "[percent_complete][start_time][elapsed_time][cpu][reads][writes]" +
        "[tempdb_current][tempdb_allocations][open_tran_count][sql_text][sql_command]" +
        "[database_name][program_name][host_name][login_name]";

    internal const string CompactOutputColumnList =
        "[session_id][status][wait_info][blocking_session_id][blocked_session_count]" +
        "[start_time][elapsed_time][cpu][reads][writes]" +
        "[sql_text][database_name][program_name]";

    public WhoIsActiveService(
        IOptions<SqlAugurOptions> options,
        ILogger<WhoIsActiveService> logger)
        : base(options, logger, AllowedProcedures, BlockedParameters,
            "output/schema parameters are blocked",
            "sp_WhoIsActive must be installed. " +
            "See: https://github.com/amachanic/sp_whoisactive")
    {
    }

    public async Task<string> ExecuteWhoIsActiveAsync(
        string serverName,
        string? filter,
        string? filterType,
        string? notFilter,
        string? notFilterType,
        bool? showOwnSpid,
        bool? showSystemSpids,
        int? showSleepingSpids,
        bool? getFullInnerText,
        int? getPlans,
        bool? getOuterCommand,
        bool? getTransactionInfo,
        int? getTaskInfo,
        bool? getLocks,
        bool? getAvgTime,
        bool? getAdditionalInfo,
        bool? getMemoryInfo,
        bool? findBlockLeaders,
        int? deltaInterval,
        string? sortOrder,
        bool? formatOutput,
        bool? compact,
        string? outputColumnList,
        bool? verbose,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@filter", filter);
        AddIfNotNull(parameters, "@filter_type", filterType);
        AddIfNotNull(parameters, "@not_filter", notFilter);
        AddIfNotNull(parameters, "@not_filter_type", notFilterType);
        AddBoolParam(parameters, "@show_own_spid", showOwnSpid);
        AddBoolParam(parameters, "@show_system_spids", showSystemSpids);
        AddIfNotNull(parameters, "@show_sleeping_spids", showSleepingSpids);
        AddBoolParam(parameters, "@get_full_inner_text", getFullInnerText);
        AddIfNotNull(parameters, "@get_plans", getPlans);
        AddBoolParam(parameters, "@get_outer_command", getOuterCommand);
        AddBoolParam(parameters, "@get_transaction_info", getTransactionInfo);
        AddIfNotNull(parameters, "@get_task_info", getTaskInfo);
        AddBoolParam(parameters, "@get_locks", getLocks);
        AddBoolParam(parameters, "@get_avg_time", getAvgTime ?? true);
        AddBoolParam(parameters, "@get_additional_info", getAdditionalInfo);
        AddBoolParam(parameters, "@get_memory_info", getMemoryInfo);
        AddBoolParam(parameters, "@find_block_leaders", findBlockLeaders ?? true);
        AddIfNotNull(parameters, "@delta_interval", deltaInterval);
        AddIfNotNull(parameters, "@sort_order", sortOrder);
        AddBoolParam(parameters, "@format_output", formatOutput ?? false);

        // Set output column list: explicit override > compact > verbose > default
        if (verbose != true)
        {
            if (outputColumnList is not null)
                parameters["@output_column_list"] = outputColumnList;
            else if (compact == true)
                parameters["@output_column_list"] = CompactOutputColumnList;
            else
                parameters["@output_column_list"] = DefaultOutputColumnList;
        }

        var formatOptions = BuildWhoIsActiveOptions(compact, verbose);
        return await ExecuteProcedureAsync(serverName, "sp_WhoIsActive", parameters, formatOptions, cancellationToken);
    }

    // ───────────────────────────────────────────────
    // Format option factories (internal static for testability)
    // ───────────────────────────────────────────────

    internal static ResultSetFormatOptions BuildWhoIsActiveOptions(bool? compact, bool? verbose)
    {
        if (verbose == true)
            return new ResultSetFormatOptions { MaxStringLength = int.MaxValue };

        if (compact == true)
        {
            return new ResultSetFormatOptions
            {
                MaxStringLength = 500
            };
        }

        return new ResultSetFormatOptions
        {
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["sql_text"] = 4000,
                ["sql_command"] = 4000,
                ["query_plan"] = 500,
                ["locks"] = 2000,
                ["additional_info"] = 2000,
                ["memory_info"] = 1000
            }
        };
    }
}
