using ModelContextProtocol;
using SqlServerMcp.Services;
using SqlServerMcp.Tools;

namespace SqlServerMcp.Tests;

public class BlitzLockToolTests
{
    private readonly StubFirstResponderService _stub = new();
    private readonly BlitzLockTool _tool;

    public BlitzLockToolTests()
    {
        _tool = new BlitzLockTool(_stub, new NoOpRateLimiter());
    }

    [Fact]
    public async Task ValidStartDate_ParsedCorrectly()
    {
        await _tool.BlitzLock("srv", startDate: "2024-06-15", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(_stub.CapturedStartDate);
        Assert.Equal(new DateTime(2024, 6, 15), _stub.CapturedStartDate);
    }

    [Fact]
    public async Task InvalidStartDate_ThrowsMcpException()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.BlitzLock("srv", startDate: "not-a-date", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Invalid start date", ex.Message);
    }

    [Fact]
    public async Task InvalidEndDate_ThrowsMcpException()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.BlitzLock("srv", endDate: "not-a-date", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Invalid end date", ex.Message);
    }

    [Fact]
    public async Task NullDates_PassedAsNull()
    {
        await _tool.BlitzLock("srv", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(_stub.CapturedStartDate);
        Assert.Null(_stub.CapturedEndDate);
    }

    [Fact]
    public async Task ArgumentException_WrappedAsMcpException()
    {
        _stub.ExceptionToThrow = new ArgumentException("Server 'bad' not found.");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.BlitzLock("bad", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Server 'bad' not found", ex.Message);
    }

    [Fact]
    public async Task InvalidOperationException_WrappedAsMcpException()
    {
        _stub.ExceptionToThrow = new InvalidOperationException("Procedure not installed.");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.BlitzLock("srv", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Procedure not installed", ex.Message);
    }

    private sealed class StubFirstResponderService : IFirstResponderService
    {
        public DateTime? CapturedStartDate { get; private set; }
        public DateTime? CapturedEndDate { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> ExecuteBlitzLockAsync(
            string serverName, string? databaseName,
            DateTime? startDate, DateTime? endDate,
            string? objectName, string? storedProcName,
            string? appName, string? hostName, string? loginName,
            bool? victimsOnly, string? eventSessionName,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            CapturedStartDate = startDate;
            CapturedEndDate = endDate;
            return Task.FromResult("ok");
        }

        public Task<string> ExecuteBlitzAsync(string serverName, bool? checkUserDatabaseObjects, bool? checkServerInfo, int? ignorePrioritiesAbove, bool? bringThePain, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzFirstAsync(string serverName, int? seconds, bool? expertMode, bool? showSleepingSpids, bool? sinceStartup, int? fileLatencyThresholdMs, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzCacheAsync(string serverName, string? sortOrder, int? top, bool? expertMode, string? databaseName, string? slowlySearchPlansFor, bool? exportToExcel, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzIndexAsync(string serverName, string? databaseName, string? schemaName, string? tableName, bool? getAllDatabases, int? mode, int? thresholdMb, int? filter, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzWhoAsync(string serverName, bool? expertMode, bool? showSleepingSpids, int? minElapsedSeconds, int? minCpuTime, int? minLogicalReads, int? minBlockingSeconds, int? minTempdbMb, bool? showActualParameters, bool? getLiveQueryPlan, string? sortOrder, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
