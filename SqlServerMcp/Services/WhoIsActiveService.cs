using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

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

    public WhoIsActiveService(
        IOptions<SqlServerMcpOptions> options,
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
        AddBoolParam(parameters, "@get_avg_time", getAvgTime);
        AddBoolParam(parameters, "@get_additional_info", getAdditionalInfo);
        AddBoolParam(parameters, "@get_memory_info", getMemoryInfo);
        AddBoolParam(parameters, "@find_block_leaders", findBlockLeaders);
        AddIfNotNull(parameters, "@delta_interval", deltaInterval);
        AddIfNotNull(parameters, "@sort_order", sortOrder);
        AddBoolParam(parameters, "@format_output", formatOutput);

        return await ExecuteProcedureAsync(serverName, "sp_WhoIsActive", parameters, cancellationToken);
    }
}
