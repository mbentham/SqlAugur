using System.Text.Json;
using SqlServerMcp.IntegrationTests.Fixtures;

namespace SqlServerMcp.IntegrationTests;

[Collection("Database")]
public sealed class SqlServerServiceIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private const string Server = SqlServerContainerFixture.ServerName;
    private const string Db = SqlServerContainerFixture.TestDatabaseName;

    public SqlServerServiceIntegrationTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ───────────────────────────────────────────────
    // Query execution — result structure
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteQuery_SimpleSelect_ReturnsExpectedJsonStructure()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var json = await service.ExecuteQueryAsync(Server, Db,
            "SELECT ProductId, Name, Price FROM dbo.Products ORDER BY ProductId",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("truncated").GetBoolean());

        var rows = root.GetProperty("rows");
        Assert.Equal(3, rows.GetArrayLength());
        Assert.Equal("Laptop", rows[0].GetProperty("Name").GetString());
        Assert.Equal(999.99m, rows[0].GetProperty("Price").GetDecimal());
    }

    [Fact]
    public async Task ExecuteQuery_NullValue_SerializedAsJsonNull()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var json = await service.ExecuteQueryAsync(Server, Db,
            "SELECT Name, Description FROM dbo.Categories WHERE Name = 'Books'",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("Description").ValueKind);
    }

    [Fact]
    public async Task ExecuteQuery_MaxRowsTruncation_SetsTruncatedFlag()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString, maxRows: 2);

        var json = await service.ExecuteQueryAsync(Server, Db,
            "SELECT ProductId FROM dbo.Products ORDER BY ProductId",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(2, root.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteQuery_CteQuery_Succeeds()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var json = await service.ExecuteQueryAsync(Server, Db, """
            WITH ProductCounts AS (
                SELECT CategoryId, COUNT(*) AS ProductCount
                FROM dbo.Products
                GROUP BY CategoryId
            )
            SELECT c.Name, pc.ProductCount
            FROM dbo.Categories c
            INNER JOIN ProductCounts pc ON pc.CategoryId = c.CategoryId
            ORDER BY c.Name
            """, CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
    }

    [Fact]
    public async Task ExecuteQuery_CrossSchemaJoin_Succeeds()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var json = await service.ExecuteQueryAsync(Server, Db, """
            SELECT oi.OrderItemId, p.Name, o.CustomerName
            FROM sales.OrderItems oi
            INNER JOIN dbo.Products p ON p.ProductId = oi.ProductId
            INNER JOIN sales.Orders o ON o.OrderId = oi.OrderId
            ORDER BY oi.OrderItemId
            """, CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteQuery_DateTimeColumn_FormattedAsIso8601()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var json = await service.ExecuteQueryAsync(Server, Db,
            "SELECT CreatedAt FROM dbo.Products WHERE ProductId = 1",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        var createdAt = rows[0].GetProperty("CreatedAt").GetString()!;

        // ISO 8601 format: contains 'T' separator and has datetime-like structure
        Assert.Contains("T", createdAt);
        Assert.True(DateTime.TryParse(createdAt, out _), $"Expected ISO 8601 datetime, got: {createdAt}");
    }

    [Fact]
    public async Task ExecuteQuery_BlockedQuery_RejectedBeforeHittingDb()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteQueryAsync(Server, Db,
                "DROP TABLE dbo.Products", CancellationToken.None));
    }

    // ───────────────────────────────────────────────
    // ListDatabasesAsync
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ListDatabases_ReturnsMasterAndTestDb()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var result = await service.ListDatabasesAsync(Server, CancellationToken.None);

        Assert.Contains("master", result);
        Assert.Contains(Db, result);
    }

    // ───────────────────────────────────────────────
    // Query plans
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetEstimatedPlan_ReturnsShowPlanXml()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var result = await service.GetEstimatedPlanAsync(Server, Db,
            "SELECT * FROM dbo.Products WHERE ProductId = 1",
            CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Contains("ShowPlanXML", result);
    }

    [Fact]
    public async Task GetActualPlan_ReturnsShowPlanXml()
    {
        var service = ServiceFactory.CreateSqlServerService(_fixture.ConnectionString);

        var result = await service.GetActualPlanAsync(Server, Db,
            "SELECT * FROM dbo.Products WHERE ProductId = 1",
            CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Contains("ShowPlanXML", result);
    }
}
