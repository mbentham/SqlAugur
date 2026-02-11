using SqlServerMcp.Services;
using SqlServerMcp.Tools;

namespace SqlServerMcp.Tests;

public class DiscoveryToolsTests
{
    private readonly DiscoveryTools _tools;
    private readonly StubToolsetManager _stubManager;

    public DiscoveryToolsTests()
    {
        _stubManager = new StubToolsetManager();
        _tools = new DiscoveryTools(_stubManager);
    }

    [Fact]
    public void ListToolsets_DelegatesToManager()
    {
        _stubManager.SummariesResult = """[{"name":"test"}]""";

        var result = _tools.ListToolsets();

        Assert.Equal("""[{"name":"test"}]""", result);
        Assert.True(_stubManager.GetSummariesCalled);
    }

    [Fact]
    public void GetToolsetTools_DelegatesToManager()
    {
        _stubManager.DetailsResult = """{"toolset":"frk"}""";

        var result = _tools.GetToolsetTools("first_responder_kit");

        Assert.Equal("""{"toolset":"frk"}""", result);
        Assert.Equal("first_responder_kit", _stubManager.LastDetailsName);
    }

    [Fact]
    public void EnableToolset_DelegatesToManager()
    {
        _stubManager.EnableResult = """{"status":"enabled"}""";

        var result = _tools.EnableToolset("darling_data");

        Assert.Equal("""{"status":"enabled"}""", result);
        Assert.Equal("darling_data", _stubManager.LastEnableName);
    }

    [Fact]
    public void GetToolsetTools_PassesExactToolsetName()
    {
        _stubManager.DetailsResult = "{}";

        _tools.GetToolsetTools("whoisactive");

        Assert.Equal("whoisactive", _stubManager.LastDetailsName);
    }

    [Fact]
    public void EnableToolset_PassesExactToolsetName()
    {
        _stubManager.EnableResult = "{}";

        _tools.EnableToolset("first_responder_kit");

        Assert.Equal("first_responder_kit", _stubManager.LastEnableName);
    }

    private sealed class StubToolsetManager : IToolsetManager
    {
        public string SummariesResult { get; set; } = "[]";
        public string DetailsResult { get; set; } = "{}";
        public string EnableResult { get; set; } = "{}";

        public bool GetSummariesCalled { get; private set; }
        public string? LastDetailsName { get; private set; }
        public string? LastEnableName { get; private set; }

        public string GetToolsetSummaries()
        {
            GetSummariesCalled = true;
            return SummariesResult;
        }

        public string GetToolsetDetails(string toolsetName)
        {
            LastDetailsName = toolsetName;
            return DetailsResult;
        }

        public string EnableToolset(string toolsetName)
        {
            LastEnableName = toolsetName;
            return EnableResult;
        }
    }
}
