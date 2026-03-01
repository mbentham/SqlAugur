using System.Text.Json;
using SqlAugur.IntegrationTests.Fixtures;

namespace SqlAugur.IntegrationTests;

[Collection("Database")]
public sealed class SchemaExplorationServiceIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private const string Server = SqlServerContainerFixture.ServerName;
    private const string Db = SqlServerContainerFixture.TestDatabaseName;

    public SchemaExplorationServiceIntegrationTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ───────────────────────────────────────────────
    // ListProgrammableObjects
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ListProgrammableObjects_AllTypes_ReturnsViewProcAndFunction()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.ListProgrammableObjectsAsync(Server, Db,
            includeSchemas: null, excludeSchemas: null, objectTypes: null,
            CancellationToken.None);

        var objects = JsonDocument.Parse(result).RootElement;
        Assert.True(objects.GetArrayLength() >= 4);

        var names = objects.EnumerateArray()
            .Select(o => o.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("vw_ActiveProducts", names);
        Assert.Contains("usp_GetProductsByCategory", names);
        Assert.Contains("fn_GetCategoryName", names);
    }

    [Fact]
    public async Task ListProgrammableObjects_FilterByType_ReturnsOnlyViews()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.ListProgrammableObjectsAsync(Server, Db,
            includeSchemas: null, excludeSchemas: null, objectTypes: ["VIEW"],
            CancellationToken.None);

        var objects = JsonDocument.Parse(result).RootElement;

        foreach (var obj in objects.EnumerateArray())
        {
            Assert.Equal("VIEW", obj.GetProperty("type").GetString());
        }

        var names = objects.EnumerateArray()
            .Select(o => o.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("vw_ActiveProducts", names);
    }

    [Fact]
    public async Task ListProgrammableObjects_FilterBySchema_ReturnsOnlyDbo()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.ListProgrammableObjectsAsync(Server, Db,
            includeSchemas: ["dbo"], excludeSchemas: null, objectTypes: null,
            CancellationToken.None);

        var objects = JsonDocument.Parse(result).RootElement;

        foreach (var obj in objects.EnumerateArray())
        {
            Assert.Equal("dbo", obj.GetProperty("schema").GetString());
        }
    }

    [Fact]
    public async Task ListProgrammableObjects_HasExpectedFields()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.ListProgrammableObjectsAsync(Server, Db,
            includeSchemas: null, excludeSchemas: null, objectTypes: null,
            CancellationToken.None);

        var objects = JsonDocument.Parse(result).RootElement;
        var first = objects.EnumerateArray().First();

        Assert.NotNull(first.GetProperty("schema").GetString());
        Assert.NotNull(first.GetProperty("name").GetString());
        Assert.NotNull(first.GetProperty("type").GetString());
    }

    // ───────────────────────────────────────────────
    // GetObjectDefinition
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetObjectDefinition_View_ReturnsDefinition()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDefinitionAsync(Server, Db,
            "vw_ActiveProducts", "dbo", CancellationToken.None);

        Assert.Contains("## VIEW:", result);
        Assert.Contains("[dbo].[vw_ActiveProducts]", result);
        Assert.Contains("```sql", result);
        Assert.Contains("SELECT", result);
        Assert.Contains("Products", result);
    }

    [Fact]
    public async Task GetObjectDefinition_Procedure_ReturnsDefinition()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDefinitionAsync(Server, Db,
            "usp_GetProductsByCategory", "dbo", CancellationToken.None);

        Assert.Contains("## PROCEDURE:", result);
        Assert.Contains("```sql", result);
        Assert.Contains("@CategoryId", result);
    }

    [Fact]
    public async Task GetObjectDefinition_NonexistentObject_ThrowsArgumentException()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetObjectDefinitionAsync(Server, Db,
                "nonexistent_proc", "dbo", CancellationToken.None));
    }

    // ───────────────────────────────────────────────
    // GetExtendedProperties
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetExtendedProperties_AllProperties_ReturnsSeededData()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetExtendedPropertiesAsync(Server, Db,
            null, null, null, CancellationToken.None);

        var properties = JsonDocument.Parse(result).RootElement;
        Assert.True(properties.GetArrayLength() >= 2);

        var tableDescriptions = properties.EnumerateArray()
            .Where(p => p.GetProperty("propertyName").GetString() == "MS_Description"
                     && p.GetProperty("table").GetString() == "Products")
            .ToList();

        // Should have both table-level and column-level properties
        Assert.True(tableDescriptions.Count >= 2);
    }

    [Fact]
    public async Task GetExtendedProperties_FilterByTable_ReturnsOnlyProducts()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetExtendedPropertiesAsync(Server, Db,
            "dbo", "Products", null, CancellationToken.None);

        var properties = JsonDocument.Parse(result).RootElement;

        foreach (var prop in properties.EnumerateArray())
        {
            Assert.Equal("Products", prop.GetProperty("table").GetString());
        }
    }

    [Fact]
    public async Task GetExtendedProperties_FilterByColumn_ReturnsColumnProperty()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetExtendedPropertiesAsync(Server, Db,
            "dbo", "Products", "Name", CancellationToken.None);

        var properties = JsonDocument.Parse(result).RootElement;
        Assert.True(properties.GetArrayLength() >= 1);

        var prop = properties.EnumerateArray().First();
        Assert.Equal("Name", prop.GetProperty("column").GetString());
        Assert.Contains("Product display name", prop.GetProperty("propertyValue").GetString());
    }

    [Fact]
    public async Task GetExtendedProperties_NoProperties_ReturnsEmptyArray()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetExtendedPropertiesAsync(Server, Db,
            "sales", null, null, CancellationToken.None);

        var properties = JsonDocument.Parse(result).RootElement;
        Assert.Equal(0, properties.GetArrayLength());
    }

    // ───────────────────────────────────────────────
    // GetObjectDependencies
    // ───────────────────────────────────────────────

    [Fact]
    public async Task GetObjectDependencies_View_ReferencesTablesAndColumns()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDependenciesAsync(Server, Db,
            "vw_ActiveProducts", "dbo", CancellationToken.None);

        var deps = JsonDocument.Parse(result).RootElement;
        var references = deps.GetProperty("references");

        Assert.True(references.GetArrayLength() >= 2);

        var refNames = references.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("Products", refNames);
        Assert.Contains("Categories", refNames);
    }

    [Fact]
    public async Task GetObjectDependencies_Procedure_ReferencesProducts()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDependenciesAsync(Server, Db,
            "usp_GetProductsByCategory", "dbo", CancellationToken.None);

        var deps = JsonDocument.Parse(result).RootElement;
        var references = deps.GetProperty("references");

        var refNames = references.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("Products", refNames);
    }

    [Fact]
    public async Task GetObjectDependencies_Table_ReferencedByViewAndProc()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDependenciesAsync(Server, Db,
            "Products", "dbo", CancellationToken.None);

        var deps = JsonDocument.Parse(result).RootElement;
        var referencedBy = deps.GetProperty("referencedBy");

        var refByNames = referencedBy.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("vw_ActiveProducts", refByNames);
        Assert.Contains("usp_GetProductsByCategory", refByNames);
    }

    [Fact]
    public async Task GetObjectDependencies_NoReferences_ReturnsEmptyArrays()
    {
        var service = ServiceFactory.CreateSchemaExplorationService(_fixture.ConnectionString);

        var result = await service.GetObjectDependenciesAsync(Server, Db,
            "Categories", "dbo", CancellationToken.None);

        var deps = JsonDocument.Parse(result).RootElement;
        // Categories is referenced by other objects but references nothing itself
        Assert.Equal(0, deps.GetProperty("references").GetArrayLength());
    }
}
