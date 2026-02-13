using ModelContextProtocol;
using SqlServerMcp.Services;
using SqlServerMcp.Tools;

namespace SqlServerMcp.Tests;

public class QueryPlanToolTests : IDisposable
{
    private readonly StubSqlServerService _stub = new();
    private readonly QueryPlanTool _tool;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"qpt_{Guid.NewGuid():N}");

    public QueryPlanToolTests()
    {
        _tool = new QueryPlanTool(_stub, new NoOpRateLimiter());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile() => Path.Combine(_tempDir, "plan.sqlplan");

    [Fact]
    public async Task EstimatedPlanType_CallsEstimatedMethod()
    {
        var result = await _tool.GetQueryPlan("srv", "SELECT 1", "mydb", TempFile(), planType: "estimated", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(_stub.EstimatedCalled);
        Assert.False(_stub.ActualCalled);
        Assert.Contains("Execution plan saved to", result);
    }

    [Fact]
    public async Task ActualPlanType_CallsActualMethod()
    {
        var result = await _tool.GetQueryPlan("srv", "SELECT 1", "mydb", TempFile(), planType: "actual", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(_stub.ActualCalled);
        Assert.False(_stub.EstimatedCalled);
        Assert.Contains("Execution plan saved to", result);
    }

    [Fact]
    public async Task InvalidPlanType_ThrowsMcpException()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.GetQueryPlan("srv", "SELECT 1", "mydb", TempFile(), planType: "invalid", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("planType", ex.Message);
    }

    [Fact]
    public async Task CaseInsensitivePlanType_Works()
    {
        await _tool.GetQueryPlan("srv", "SELECT 1", "mydb", TempFile(), planType: "ESTIMATED", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(_stub.EstimatedCalled);
    }

    [Fact]
    public async Task ArgumentException_WrappedAsMcpException()
    {
        _stub.ExceptionToThrow = new ArgumentException("Server 'bad' not found.");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tool.GetQueryPlan("bad", "SELECT 1", "mydb", TempFile(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Server 'bad' not found", ex.Message);
    }

    private sealed class StubSqlServerService : ISqlServerService
    {
        public bool EstimatedCalled { get; private set; }
        public bool ActualCalled { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public IReadOnlyList<string> GetServerNames() => [];

        public Task<string> ExecuteQueryAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> ListDatabasesAsync(string serverName, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<string> GetEstimatedPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            EstimatedCalled = true;
            return Task.FromResult("estimated-plan");
        }

        public Task<string> GetActualPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            ActualCalled = true;
            return Task.FromResult("actual-plan");
        }
    }
}
