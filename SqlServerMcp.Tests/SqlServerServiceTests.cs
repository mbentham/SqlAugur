using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class SqlServerServiceTests
{
    private readonly SqlServerService _service;

    public SqlServerServiceTests()
    {
        var options = Options.Create(new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["testserver"] = new() { ConnectionString = "Server=localhost;Database=master;Encrypt=True;TrustServerCertificate=False;" }
            },
            MaxRows = 100,
            CommandTimeoutSeconds = 30
        });

        _service = new SqlServerService(options, NullLogger<SqlServerService>.Instance);
    }

    // ───────────────────────────────────────────────
    // GetServerNames
    // ───────────────────────────────────────────────

    [Fact]
    public void GetServerNames_ReturnsConfiguredServers()
    {
        var servers = _service.GetServerNames();

        Assert.Single(servers);
        Assert.Contains("testserver", servers);
    }

    [Fact]
    public void GetServerNames_ReturnsOrderedList()
    {
        var options = Options.Create(new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["zebra"] = new() { ConnectionString = "Server=z;" },
                ["alpha"] = new() { ConnectionString = "Server=a;" },
                ["beta"] = new() { ConnectionString = "Server=b;" }
            }
        });

        var service = new SqlServerService(options, NullLogger<SqlServerService>.Instance);
        var servers = service.GetServerNames();

        Assert.Equal(3, servers.Count);
        Assert.Equal("alpha", servers[0]);
        Assert.Equal("beta", servers[1]);
        Assert.Equal("zebra", servers[2]);
    }

    // ───────────────────────────────────────────────
    // ExecuteQueryAsync - Validation
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteQuery_InvalidQuery_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExecuteQueryAsync("testserver", "master", "DROP TABLE users", CancellationToken.None));

        Assert.Contains("SELECT", ex.Message);
    }

    [Fact]
    public async Task ExecuteQuery_EmptyQuery_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExecuteQueryAsync("testserver", "master", "", CancellationToken.None));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteQuery_MultipleStatements_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExecuteQueryAsync("testserver", "master", "SELECT 1; SELECT 2", CancellationToken.None));

        Assert.Contains("Multiple", ex.Message);
    }

    [Fact]
    public async Task ExecuteQuery_SelectInto_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExecuteQueryAsync("testserver", "master", "SELECT * INTO #temp FROM sys.objects", CancellationToken.None));

        Assert.Contains("SELECT INTO", ex.Message);
    }

    // ───────────────────────────────────────────────
    // ExecuteQueryAsync - Server Resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteQuery_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteQueryAsync("nonexistent", "master", "SELECT 1", CancellationToken.None));

        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("testserver", ex.Message);
    }

    // ───────────────────────────────────────────────
    // GetEstimatedPlanAsync - Validation
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetEstimatedPlan_InvalidQuery_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetEstimatedPlanAsync("testserver", "master", "UPDATE users SET name = 'x'", CancellationToken.None));

        Assert.Contains("SELECT", ex.Message);
    }

    [Fact]
    public async Task GetEstimatedPlan_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEstimatedPlanAsync("bad", "master", "SELECT 1", CancellationToken.None));

        Assert.Contains("bad", ex.Message);
    }

    // ───────────────────────────────────────────────
    // GetActualPlanAsync - Validation
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetActualPlan_InvalidQuery_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetActualPlanAsync("testserver", "master", "DELETE FROM users", CancellationToken.None));

        Assert.Contains("SELECT", ex.Message);
    }

    [Fact]
    public async Task GetActualPlan_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetActualPlanAsync("bad", "master", "SELECT 1", CancellationToken.None));

        Assert.Contains("bad", ex.Message);
    }

    // ───────────────────────────────────────────────
    // ListDatabasesAsync - Server Resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ListDatabases_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ListDatabasesAsync("nonexistent", CancellationToken.None));

        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("testserver", ex.Message);
    }
}
