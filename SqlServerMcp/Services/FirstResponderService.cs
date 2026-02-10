using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

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
        IOptions<SqlServerMcpOptions> options,
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
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddBoolParam(parameters, "@CheckUserDatabaseObjects", checkUserDatabaseObjects);
        AddBoolParam(parameters, "@CheckServerInfo", checkServerInfo);
        AddIfNotNull(parameters, "@IgnorePrioritiesAbove", ignorePrioritiesAbove);
        AddBoolParam(parameters, "@BringThePain", bringThePain);

        return await ExecuteProcedureAsync(serverName, "sp_Blitz", parameters, cancellationToken);
    }

    public async Task<string> ExecuteBlitzFirstAsync(
        string serverName,
        int? seconds,
        bool? expertMode,
        bool? showSleepingSpids,
        bool? sinceStartup,
        int? fileLatencyThresholdMs,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@Seconds", seconds);
        AddBoolParam(parameters, "@ExpertMode", expertMode);
        AddBoolParam(parameters, "@ShowSleepingSPIDs", showSleepingSpids);
        AddBoolParam(parameters, "@SinceStartup", sinceStartup);
        AddIfNotNull(parameters, "@FileLatencyThresholdMS", fileLatencyThresholdMs);

        return await ExecuteProcedureAsync(serverName, "sp_BlitzFirst", parameters, cancellationToken);
    }

    public async Task<string> ExecuteBlitzCacheAsync(
        string serverName,
        string? sortOrder,
        int? top,
        bool? expertMode,
        string? databaseName,
        string? slowlySearchPlansFor,
        bool? exportToExcel,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@SortOrder", sortOrder);
        AddIfNotNull(parameters, "@Top", top);
        AddBoolParam(parameters, "@ExpertMode", expertMode);
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        AddIfNotNull(parameters, "@SlowlySearchPlansFor", slowlySearchPlansFor);
        AddBoolParam(parameters, "@ExportToExcel", exportToExcel);

        return await ExecuteProcedureAsync(serverName, "sp_BlitzCache", parameters, cancellationToken);
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

        return await ExecuteProcedureAsync(serverName, "sp_BlitzIndex", parameters, cancellationToken);
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

        return await ExecuteProcedureAsync(serverName, "sp_BlitzWho", parameters, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "@DatabaseName", databaseName);
        AddIfNotNull(parameters, "@StartDate", startDate);
        AddIfNotNull(parameters, "@EndDate", endDate);
        AddIfNotNull(parameters, "@ObjectName", objectName);
        AddIfNotNull(parameters, "@StoredProcName", storedProcName);
        AddIfNotNull(parameters, "@AppName", appName);
        AddIfNotNull(parameters, "@HostName", hostName);
        AddIfNotNull(parameters, "@LoginName", loginName);
        AddBoolParam(parameters, "@VictimsOnly", victimsOnly);
        AddIfNotNull(parameters, "@EventSessionName", eventSessionName);

        return await ExecuteProcedureAsync(serverName, "sp_BlitzLock", parameters, cancellationToken);
    }
}
