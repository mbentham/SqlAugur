namespace SqlAugur.Services;

public interface IFirstResponderService
{
    Task<string> ExecuteBlitzAsync(
        string serverName,
        bool? checkUserDatabaseObjects,
        bool? checkServerInfo,
        int? ignorePrioritiesAbove,
        bool? bringThePain,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken);

    Task<string> ExecuteBlitzFirstAsync(
        string serverName,
        int? seconds,
        bool? expertMode,
        bool? showSleepingSpids,
        bool? sinceStartup,
        int? fileLatencyThresholdMs,
        bool? includeQueryPlans,
        bool? verbose,
        string? resultSets,
        CancellationToken cancellationToken);

    Task<string> ExecuteBlitzCacheAsync(
        string serverName,
        string? sortOrder,
        int? top,
        bool? expertMode,
        string? databaseName,
        string? slowlySearchPlansFor,
        bool? exportToExcel,
        bool? includeQueryPlans,
        bool? verbose,
        CancellationToken cancellationToken);

    Task<string> ExecuteBlitzIndexAsync(
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
        CancellationToken cancellationToken);

    Task<string> ExecuteBlitzWhoAsync(
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
        CancellationToken cancellationToken);

    Task<string> ExecuteBlitzLockAsync(
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
        CancellationToken cancellationToken);
}
