using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Configuration;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class ToolsetManagerTests
{
    private static ToolsetManager CreateManager(
        SqlServerMcpOptions? config = null,
        McpServerOptions? mcpOptions = null,
        IServiceProvider? serviceProvider = null)
    {
        config ??= new SqlServerMcpOptions();
        mcpOptions ??= new McpServerOptions();
        serviceProvider ??= new ServiceCollection().BuildServiceProvider();

        return new ToolsetManager(
            Options.Create(mcpOptions),
            Options.Create(config),
            serviceProvider,
            NullLogger<ToolsetManager>.Instance);
    }

    private static IServiceProvider BuildServiceProviderWithDbaServices()
    {
        var sqlServerOptions = Options.Create(new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["testserver"] = new() { ConnectionString = "Server=test-only-no-connection;Database=master;Encrypt=True;TrustServerCertificate=False;" }
            },
            MaxRows = 100,
            CommandTimeoutSeconds = 120,
            EnableFirstResponderKit = true,
            EnableDarlingData = true,
            EnableWhoIsActive = true,
        });

        var services = new ServiceCollection();
        services.AddSingleton(sqlServerOptions);
        services.AddSingleton<IOptions<SqlServerMcpOptions>>(sqlServerOptions);
        services.AddSingleton<IRateLimitingService, NoOpRateLimiter>();
        services.AddSingleton<IFirstResponderService>(sp =>
            new FirstResponderService(sqlServerOptions, NullLogger<FirstResponderService>.Instance));
        services.AddSingleton<IDarlingDataService>(sp =>
            new DarlingDataService(sqlServerOptions, NullLogger<DarlingDataService>.Instance));
        services.AddSingleton<IWhoIsActiveService>(sp =>
            new WhoIsActiveService(sqlServerOptions, NullLogger<WhoIsActiveService>.Instance));
        return services.BuildServiceProvider();
    }

    // ───────────────────────────────────────────────
    // GetToolsetSummaries
    // ───────────────────────────────────────────────

    [Fact]
    public void GetToolsetSummaries_AllConfigured_ReturnsThreeAvailable()
    {
        var manager = CreateManager(new SqlServerMcpOptions
        {
            EnableFirstResponderKit = true,
            EnableDarlingData = true,
            EnableWhoIsActive = true,
        });

        var result = manager.GetToolsetSummaries();
        var summaries = JsonDocument.Parse(result).RootElement;

        Assert.Equal(3, summaries.GetArrayLength());
        foreach (var summary in summaries.EnumerateArray())
        {
            Assert.Equal("available", summary.GetProperty("status").GetString());
            Assert.True(summary.GetProperty("toolCount").GetInt32() > 0);
        }
    }

    [Fact]
    public void GetToolsetSummaries_NoneConfigured_ReturnsThreeNotConfigured()
    {
        var manager = CreateManager(new SqlServerMcpOptions());

        var result = manager.GetToolsetSummaries();
        var summaries = JsonDocument.Parse(result).RootElement;

        Assert.Equal(3, summaries.GetArrayLength());
        foreach (var summary in summaries.EnumerateArray())
        {
            Assert.Equal("not_configured", summary.GetProperty("status").GetString());
        }
    }

    [Fact]
    public void GetToolsetSummaries_MixedConfig_ReturnsCorrectStatuses()
    {
        var manager = CreateManager(new SqlServerMcpOptions
        {
            EnableFirstResponderKit = true,
            EnableDarlingData = false,
            EnableWhoIsActive = true,
        });

        var result = manager.GetToolsetSummaries();
        var summaries = JsonDocument.Parse(result).RootElement;

        var byName = new Dictionary<string, string>();
        foreach (var summary in summaries.EnumerateArray())
            byName[summary.GetProperty("name").GetString()!] = summary.GetProperty("status").GetString()!;

        Assert.Equal("available", byName["first_responder_kit"]);
        Assert.Equal("not_configured", byName["darling_data"]);
        Assert.Equal("available", byName["whoisactive"]);
    }

    [Fact]
    public void GetToolsetSummaries_AfterEnable_ReturnsEnabled()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableFirstResponderKit = true },
            mcpOptions,
            sp);

        manager.EnableToolset("first_responder_kit");
        var result = manager.GetToolsetSummaries();
        var summaries = JsonDocument.Parse(result).RootElement;

        var frkStatus = summaries.EnumerateArray()
            .First(s => s.GetProperty("name").GetString() == "first_responder_kit")
            .GetProperty("status").GetString();

        Assert.Equal("enabled", frkStatus);
    }

    [Fact]
    public void GetToolsetSummaries_IncludesToolNames()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableFirstResponderKit = true });

        var result = manager.GetToolsetSummaries();
        var summaries = JsonDocument.Parse(result).RootElement;

        var frk = summaries.EnumerateArray()
            .First(s => s.GetProperty("name").GetString() == "first_responder_kit");

        var toolNames = frk.GetProperty("toolNames").EnumerateArray()
            .Select(t => t.GetString()).ToList();

        Assert.Contains("sp_blitz", toolNames);
        Assert.Contains("sp_blitz_cache", toolNames);
        Assert.Equal(6, frk.GetProperty("toolCount").GetInt32());
    }

    // ───────────────────────────────────────────────
    // GetToolsetDetails
    // ───────────────────────────────────────────────

    [Fact]
    public void GetToolsetDetails_ValidName_ReturnsToolInfo()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableFirstResponderKit = true });

        var result = manager.GetToolsetDetails("first_responder_kit");
        var details = JsonDocument.Parse(result).RootElement;

        Assert.Equal("first_responder_kit", details.GetProperty("toolset").GetString());
        Assert.True(details.GetProperty("configured").GetBoolean());
        Assert.True(details.GetProperty("tools").GetArrayLength() > 0);
    }

    [Fact]
    public void GetToolsetDetails_InvalidName_ThrowsArgumentException()
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.GetToolsetDetails("nonexistent"));
    }

    [Fact]
    public void GetToolsetDetails_IncludesParameterDescriptions()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableFirstResponderKit = true });

        var result = manager.GetToolsetDetails("first_responder_kit");
        var details = JsonDocument.Parse(result).RootElement;

        var blitz = details.GetProperty("tools").EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "sp_blitz");

        // sp_blitz has serverName as a required parameter
        var serverNameParam = blitz.GetProperty("parameters").EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "serverName");

        Assert.NotNull(serverNameParam.GetProperty("description").GetString());
        Assert.Equal("string", serverNameParam.GetProperty("type").GetString());
        Assert.False(serverNameParam.GetProperty("optional").GetBoolean());
    }

    [Fact]
    public void GetToolsetDetails_WhoIsActive_ShowsAllParameters()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableWhoIsActive = true });

        var result = manager.GetToolsetDetails("whoisactive");
        var details = JsonDocument.Parse(result).RootElement;

        var tool = details.GetProperty("tools").EnumerateArray().First();
        Assert.Equal("sp_whoisactive", tool.GetProperty("name").GetString());

        // sp_WhoIsActive has many parameters
        Assert.True(tool.GetProperty("parameters").GetArrayLength() > 5);
    }

    [Fact]
    public void GetToolsetDetails_NotConfigured_ShowsConfiguredFalse()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableDarlingData = false });

        var result = manager.GetToolsetDetails("darling_data");
        var details = JsonDocument.Parse(result).RootElement;

        Assert.False(details.GetProperty("configured").GetBoolean());
    }

    // ───────────────────────────────────────────────
    // EnableToolset
    // ───────────────────────────────────────────────

    [Fact]
    public void EnableToolset_ValidAndConfigured_AddsToolsToCollection()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableFirstResponderKit = true },
            mcpOptions,
            sp);

        var result = manager.EnableToolset("first_responder_kit");
        var parsed = JsonDocument.Parse(result).RootElement;

        Assert.Equal("enabled", parsed.GetProperty("status").GetString());
        Assert.Equal(6, mcpOptions.ToolCollection.Count);
    }

    [Fact]
    public void EnableToolset_NotConfigured_ThrowsMcpException()
    {
        var manager = CreateManager(new SqlServerMcpOptions { EnableFirstResponderKit = false });

        Assert.Throws<McpException>(() => manager.EnableToolset("first_responder_kit"));
    }

    [Fact]
    public void EnableToolset_AlreadyEnabled_ReturnsIdempotentResult()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableFirstResponderKit = true },
            mcpOptions,
            sp);

        manager.EnableToolset("first_responder_kit");
        var result = manager.EnableToolset("first_responder_kit");
        var parsed = JsonDocument.Parse(result).RootElement;

        Assert.Equal("already_enabled", parsed.GetProperty("status").GetString());
        // Should not double-add tools
        Assert.Equal(6, mcpOptions.ToolCollection.Count);
    }

    [Fact]
    public void EnableToolset_InvalidName_ThrowsArgumentException()
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.EnableToolset("nonexistent"));
    }

    [Fact]
    public void EnableToolset_FRK_CorrectToolCount()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableFirstResponderKit = true },
            mcpOptions,
            sp);

        manager.EnableToolset("first_responder_kit");
        Assert.Equal(6, mcpOptions.ToolCollection.Count);
    }

    [Fact]
    public void EnableToolset_DarlingData_CorrectToolCount()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableDarlingData = true },
            mcpOptions,
            sp);

        manager.EnableToolset("darling_data");
        Assert.Equal(7, mcpOptions.ToolCollection.Count);
    }

    [Fact]
    public void EnableToolset_WhoIsActive_CorrectToolCount()
    {
        var sp = BuildServiceProviderWithDbaServices();
        var mcpOptions = new McpServerOptions { ToolCollection = [] };
        var manager = CreateManager(
            new SqlServerMcpOptions { EnableWhoIsActive = true },
            mcpOptions,
            sp);

        manager.EnableToolset("whoisactive");
        Assert.Single(mcpOptions.ToolCollection);
    }

    // ───────────────────────────────────────────────
    // Toolset definitions
    // ───────────────────────────────────────────────

    [Fact]
    public void Toolsets_ContainsAllThreeExpectedEntries()
    {
        Assert.Equal(3, ToolsetManager.Toolsets.Count);
        Assert.True(ToolsetManager.Toolsets.ContainsKey("first_responder_kit"));
        Assert.True(ToolsetManager.Toolsets.ContainsKey("darling_data"));
        Assert.True(ToolsetManager.Toolsets.ContainsKey("whoisactive"));
    }

    // ───────────────────────────────────────────────
    // GetFriendlyTypeName
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(int?), "int?")]
    [InlineData(typeof(DateTime), "DateTime")]
    [InlineData(typeof(bool?), "bool?")]
    public void GetFriendlyTypeName_ReturnsExpected(Type type, string expected)
    {
        Assert.Equal(expected, ToolsetManager.GetFriendlyTypeName(type));
    }
}
