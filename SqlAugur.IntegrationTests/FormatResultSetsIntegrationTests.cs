using System.Text.Json;
using SqlAugur.IntegrationTests.Fixtures;
using SqlAugur.Services;

namespace SqlAugur.IntegrationTests;

[Collection("Database")]
public sealed class FormatResultSetsIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private const string Server = SqlServerContainerFixture.ServerName;

    public FormatResultSetsIntegrationTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestableStoredProcedureService CreateService(int maxRows = 1000) =>
        ServiceFactory.CreateTestableService(_fixture.ConnectionString, maxRows);

    private async Task<JsonElement> ExecuteAndParse(
        TestableStoredProcedureService service,
        ResultSetFormatOptions? formatOptions)
    {
        var json = await service.CallExecuteProcedureAsync(
            Server, "usp_FormatResultSetsTest",
            new Dictionary<string, object?>(), formatOptions,
            CancellationToken.None);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task ExcludedColumn_NotInJsonOutput()
    {
        var service = CreateService();
        var options = new ResultSetFormatOptions
        {
            ExcludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QueryPlan" }
        };

        var root = await ExecuteAndParse(service, options);
        var firstRow = root[0].GetProperty("rows")[0];

        Assert.True(firstRow.TryGetProperty("Id", out _));
        Assert.True(firstRow.TryGetProperty("ShortCol", out _));
        Assert.False(firstRow.TryGetProperty("QueryPlan", out _));
    }

    [Fact]
    public async Task TruncatedColumn_EndsWithTruncatedSuffix()
    {
        var service = CreateService();
        var options = new ResultSetFormatOptions
        {
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["LargeCol"] = 100
            }
        };

        var root = await ExecuteAndParse(service, options);
        var largeCol = root[0].GetProperty("rows")[0].GetProperty("LargeCol").GetString()!;

        Assert.EndsWith("...[truncated]", largeCol);
        Assert.Equal(100 + "...[truncated]".Length, largeCol.Length);
    }

    [Fact]
    public async Task GlobalMaxString_TruncatesLargeValues_WhenNoOptionsProvided()
    {
        var service = CreateService();

        var root = await ExecuteAndParse(service, null);
        var largeCol = root[0].GetProperty("rows")[0].GetProperty("LargeCol").GetString()!;

        // With null options, global max (8000) applies
        Assert.EndsWith("...[truncated]", largeCol);
        Assert.Equal(StoredProcedureServiceBase.GlobalMaxStringLength + "...[truncated]".Length, largeCol.Length);
    }

    [Fact]
    public async Task ExcludedResultSetIndex_Skipped()
    {
        var service = CreateService();
        var options = new ResultSetFormatOptions
        {
            ExcludedResultSets = [1] // Skip second result set
        };

        var root = await ExecuteAndParse(service, options);

        // Should have 2 result sets instead of 3 (index 1 skipped)
        Assert.Equal(2, root.GetArrayLength());

        // First result set should still have Id column
        var firstRow = root[0].GetProperty("rows")[0];
        Assert.True(firstRow.TryGetProperty("Id", out _));

        // Second result set in output should be the third original (Label column)
        var secondRow = root[1].GetProperty("rows")[0];
        Assert.True(secondRow.TryGetProperty("Label", out _));
    }

    [Fact]
    public async Task MaxRowsOverride_LimitsRowCount()
    {
        var service = CreateService(maxRows: 1000);
        var options = new ResultSetFormatOptions
        {
            MaxRowsOverride = 0 // Zero rows
        };

        var root = await ExecuteAndParse(service, options);
        var rows = root[0].GetProperty("rows");
        Assert.Equal(0, rows.GetArrayLength());
        Assert.True(root[0].GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task NullOptions_ReturnsAllColumns_BackwardCompat()
    {
        var service = CreateService();

        var root = await ExecuteAndParse(service, null);
        var firstRow = root[0].GetProperty("rows")[0];

        // All columns present
        Assert.True(firstRow.TryGetProperty("Id", out _));
        Assert.True(firstRow.TryGetProperty("ShortCol", out _));
        Assert.True(firstRow.TryGetProperty("LargeCol", out _));
        Assert.True(firstRow.TryGetProperty("BinaryCol", out _));
        Assert.True(firstRow.TryGetProperty("QueryPlan", out _));

        // All 3 result sets present
        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public async Task MaxStringLengthIntMax_ReturnsEverythingUntruncated()
    {
        var service = CreateService();
        var options = new ResultSetFormatOptions
        {
            MaxStringLength = int.MaxValue
        };

        var root = await ExecuteAndParse(service, options);
        var largeCol = root[0].GetProperty("rows")[0].GetProperty("LargeCol").GetString()!;

        // 50,000 X characters, untruncated
        Assert.Equal(50000, largeCol.Length);
        Assert.DoesNotContain("...[truncated]", largeCol);
    }
}
