using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;
using SqlAugur.Services;

namespace SqlAugur.Tests;

public class BlitzPlanCompareTests
{
    // ───────────────────────────────────────────────
    // ExtractSnapshotXml — happy paths
    // ───────────────────────────────────────────────

    [Fact]
    public void ExtractSnapshotXml_HappyPath_ReturnsXmlInclusive()
    {
        var callStack = "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot Version=\"1\"><Plan /></BlitzPlanCompareSnapshot>';";

        var xml = FirstResponderService.ExtractSnapshotXml(callStack);

        Assert.Equal(
            "<BlitzPlanCompareSnapshot Version=\"1\"><Plan /></BlitzPlanCompareSnapshot>",
            xml);
    }

    [Fact]
    public void ExtractSnapshotXml_LeadingAndTrailingWhitespace_Tolerated()
    {
        var callStack = "  \r\n  EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot><P/></BlitzPlanCompareSnapshot>';  \r\n  ";

        var xml = FirstResponderService.ExtractSnapshotXml(callStack);

        Assert.StartsWith("<BlitzPlanCompareSnapshot", xml);
        Assert.EndsWith("</BlitzPlanCompareSnapshot>", xml);
    }

    [Fact]
    public void ExtractSnapshotXml_DoubledSingleQuotes_UndoubledInOutput()
    {
        // Proc-doubled: SQL ''  → real '
        var callStack = "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot><Text value=\"it''s fine\" /></BlitzPlanCompareSnapshot>';";

        var xml = FirstResponderService.ExtractSnapshotXml(callStack);

        Assert.Contains("value=\"it's fine\"", xml);
        Assert.DoesNotContain("''", xml);
    }

    [Fact]
    public void ExtractSnapshotXml_MultilineXml_Preserved()
    {
        var callStack =
            "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot>\r\n" +
            "  <Plan>\r\n" +
            "    <Node />\r\n" +
            "  </Plan>\r\n" +
            "</BlitzPlanCompareSnapshot>';";

        var xml = FirstResponderService.ExtractSnapshotXml(callStack);

        Assert.Contains("<Node />", xml);
        Assert.Contains("</Plan>", xml);
    }

    // ───────────────────────────────────────────────
    // ExtractSnapshotXml — failure modes
    // ───────────────────────────────────────────────

    [Fact]
    public void ExtractSnapshotXml_EmptyString_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => FirstResponderService.ExtractSnapshotXml(""));
        Assert.Contains("BlitzPlanCompareSnapshot", ex.Message);
    }

    [Fact]
    public void ExtractSnapshotXml_MissingOpeningTag_Throws()
    {
        var callStack = "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<NotTheRoot/></BlitzPlanCompareSnapshot>';";

        var ex = Assert.Throws<InvalidOperationException>(
            () => FirstResponderService.ExtractSnapshotXml(callStack));
        Assert.Contains("recognizable snapshot envelope", ex.Message);
    }

    [Fact]
    public void ExtractSnapshotXml_MissingClosingTag_Throws()
    {
        var callStack = "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot><Plan/>';";

        var ex = Assert.Throws<InvalidOperationException>(
            () => FirstResponderService.ExtractSnapshotXml(callStack));
        Assert.Contains("recognizable snapshot envelope", ex.Message);
    }

    [Fact]
    public void ExtractSnapshotXml_ContentFailsToParseAsXml_Throws()
    {
        // Opening and closing tags present, but body is malformed XML
        var callStack = "EXEC dbo.sp_BlitzPlanCompare @CompareToXML = N'<BlitzPlanCompareSnapshot><Unclosed></BlitzPlanCompareSnapshot>';";

        var ex = Assert.Throws<InvalidOperationException>(
            () => FirstResponderService.ExtractSnapshotXml(callStack));
        Assert.Contains("failed to parse", ex.Message);
    }

    // ───────────────────────────────────────────────
    // BuildCaptureParameters
    // ───────────────────────────────────────────────

    [Fact]
    public void BuildCaptureParameters_AllNull_ReturnsEmptyDictionary()
    {
        var parameters = FirstResponderService.BuildCaptureParameters(null, null, null, null);

        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildCaptureParameters_QueryPlanHash_SetsParameter()
    {
        var hash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var parameters = FirstResponderService.BuildCaptureParameters(hash, null, null, null);

        Assert.Same(hash, parameters["@QueryPlanHash"]);
        Assert.Single(parameters);
    }

    [Fact]
    public void BuildCaptureParameters_AllProvided_SetsAll()
    {
        var planHash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var queryHash = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 };

        var parameters = FirstResponderService.BuildCaptureParameters(planHash, queryHash, "dbo.MyProc", "MyDb");

        Assert.Equal(4, parameters.Count);
        Assert.Same(planHash, parameters["@QueryPlanHash"]);
        Assert.Same(queryHash, parameters["@QueryHash"]);
        Assert.Equal("dbo.MyProc", parameters["@StoredProcName"]);
        Assert.Equal("MyDb", parameters["@DatabaseName"]);
    }

    // ───────────────────────────────────────────────
    // ExecuteBlitzPlanCompareAsync — server resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteBlitzPlanCompareAsync_UnknownCaptureServer_ThrowsArgumentException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteBlitzPlanCompareAsync(
                "nonexistent", "testserver",
                null, null, null, null, null, null,
                TestContext.Current.CancellationToken));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public async Task ExecuteBlitzPlanCompareAsync_BothServersUnknown_CaptureResolvedFirst()
    {
        // Both servers are bogus; the capture server is resolved first, so its name
        // surfaces in the ArgumentException. A true unknown-compare-server test
        // would need a SQL test double (the compare phase only runs if capture
        // succeeds), which is deferred.
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteBlitzPlanCompareAsync(
                "nonexistent-capture", "also-nonexistent",
                null, null, null, null, null, null,
                TestContext.Current.CancellationToken));
        Assert.Contains("nonexistent-capture", ex.Message);
    }

    private static FirstResponderService CreateService()
    {
        var options = Options.Create(new SqlAugurOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["testserver"] = new() { ConnectionString = "Server=localhost;Database=master;Encrypt=True;TrustServerCertificate=False;" }
            },
            MaxRows = 100,
            CommandTimeoutSeconds = 120
        });

        return new FirstResponderService(options, NullLogger<FirstResponderService>.Instance);
    }
}
