using ModelContextProtocol;
using SqlAugur.Services;
using SqlAugur.Tools;

namespace SqlAugur.Tests;

public class BlitzPlanCompareToolTests
{
    private readonly StubFirstResponderService _stub = new();
    private readonly BlitzPlanCompareTool _tool;

    public BlitzPlanCompareToolTests()
    {
        _tool = new BlitzPlanCompareTool(_stub, new NoOpRateLimiter());
    }

    [Fact]
    public async Task ValidHexQueryPlanHash_ParsedCorrectly()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            queryPlanHash: "0x0102030405060708",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(_stub.CapturedQueryPlanHash);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, _stub.CapturedQueryPlanHash);
    }

    [Fact]
    public async Task ValidHexQueryHash_WithoutPrefix_ParsedCorrectly()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            queryHash: "0102030405060708",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, _stub.CapturedQueryHash);
    }

    [Fact]
    public async Task InvalidHexQueryPlanHash_ThrowsMcpException()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tool.BlitzPlanCompare("srvA", "srvB",
                queryPlanHash: "not-hex",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("queryPlanHash", ex.Message);
    }

    [Fact]
    public async Task WrongLengthHexQueryPlanHash_ThrowsMcpException()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tool.BlitzPlanCompare("srvA", "srvB",
                queryPlanHash: "0x0102",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("8 bytes", ex.Message);
    }

    [Fact]
    public async Task CorrectLengthButInvalidHex_ThrowsMcpException()
    {
        // 16 hex chars after strip, but 'Z' is not a valid hex digit —
        // forces the catch (FormatException) branch in ParseBinary8Hex.
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tool.BlitzPlanCompare("srvA", "srvB",
                queryPlanHash: "0x010203040506070Z",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("queryPlanHash", ex.Message);
        Assert.Contains("not a valid hex", ex.Message);
    }

    [Fact]
    public async Task NullHashes_PassedAsNull()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(_stub.CapturedQueryPlanHash);
        Assert.Null(_stub.CapturedQueryHash);
    }

    [Fact]
    public async Task StoredProcName_PassedThrough()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            storedProcName: "dbo.MyProc",
            databaseName: "MyDb",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("dbo.MyProc", _stub.CapturedStoredProcName);
        Assert.Equal("MyDb", _stub.CapturedDatabaseName);
    }

    [Fact]
    public async Task ServerNamesPassedInOrder()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("srvA", _stub.CapturedCaptureServer);
        Assert.Equal("srvB", _stub.CapturedCompareServer);
    }

    [Fact]
    public async Task IncludeQueryPlansAndVerbose_PassedThrough()
    {
        await _tool.BlitzPlanCompare("srvA", "srvB",
            includeQueryPlans: true, verbose: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(_stub.CapturedIncludeQueryPlans);
        Assert.True(_stub.CapturedVerbose);
    }

    [Fact]
    public async Task ArgumentException_WrappedAsMcpException()
    {
        _stub.ExceptionToThrow = new ArgumentException("Server 'bad' not found.");

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tool.BlitzPlanCompare("bad", "srvB",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("Server 'bad' not found", ex.Message);
    }

    [Fact]
    public async Task InvalidOperationException_WrappedAsMcpException()
    {
        _stub.ExceptionToThrow = new InvalidOperationException("no matching plan found");

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tool.BlitzPlanCompare("srvA", "srvB",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("no matching plan found", ex.Message);
    }

    private sealed class StubFirstResponderService : IFirstResponderService
    {
        public string? CapturedCaptureServer { get; private set; }
        public string? CapturedCompareServer { get; private set; }
        public byte[]? CapturedQueryPlanHash { get; private set; }
        public byte[]? CapturedQueryHash { get; private set; }
        public string? CapturedStoredProcName { get; private set; }
        public string? CapturedDatabaseName { get; private set; }
        public bool? CapturedIncludeQueryPlans { get; private set; }
        public bool? CapturedVerbose { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> ExecuteBlitzPlanCompareAsync(
            string captureServerName, string compareServerName,
            byte[]? queryPlanHash, byte[]? queryHash,
            string? storedProcName, string? databaseName,
            bool? includeQueryPlans, bool? verbose,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            CapturedCaptureServer = captureServerName;
            CapturedCompareServer = compareServerName;
            CapturedQueryPlanHash = queryPlanHash;
            CapturedQueryHash = queryHash;
            CapturedStoredProcName = storedProcName;
            CapturedDatabaseName = databaseName;
            CapturedIncludeQueryPlans = includeQueryPlans;
            CapturedVerbose = verbose;
            return Task.FromResult("ok");
        }

        public Task<string> ExecuteBlitzAsync(string serverName, bool? checkUserDatabaseObjects, bool? checkServerInfo, int? ignorePrioritiesAbove, bool? bringThePain, bool? includeQueryPlans, bool? verbose, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzFirstAsync(string serverName, int? seconds, bool? expertMode, bool? showSleepingSpids, bool? sinceStartup, int? fileLatencyThresholdMs, bool? includeQueryPlans, bool? verbose, string? resultSets, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzCacheAsync(string serverName, string? sortOrder, int? top, bool? expertMode, string? databaseName, string? slowlySearchPlansFor, bool? exportToExcel, bool? includeQueryPlans, bool? verbose, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzIndexAsync(string serverName, string? databaseName, string? schemaName, string? tableName, bool? getAllDatabases, int? mode, int? thresholdMb, int? filter, bool? includeQueryPlans, bool? verbose, int? maxRows, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzWhoAsync(string serverName, bool? expertMode, bool? showSleepingSpids, int? minElapsedSeconds, int? minCpuTime, int? minLogicalReads, int? minBlockingSeconds, int? minTempdbMb, bool? showActualParameters, bool? getLiveQueryPlan, string? sortOrder, bool? includeQueryPlans, bool? verbose, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ExecuteBlitzLockAsync(string serverName, string? databaseName, DateTime? startDate, DateTime? endDate, string? objectName, string? storedProcName, string? appName, string? hostName, string? loginName, bool? victimsOnly, string? eventSessionName, bool? includeQueryPlans, bool? includeXmlReports, bool? verbose, int? daysBack, int? maxRows, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
