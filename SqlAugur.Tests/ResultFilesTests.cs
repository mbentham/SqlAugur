using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;
using SqlAugur.Services;

namespace SqlAugur.Tests;

public sealed class ResultFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sqlaugur-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SqlServerService MakeService(int maxRows = 1000, int threshold = 500) =>
        new(Options.Create(new SqlAugurOptions
        {
            MaxRows = maxRows,
            ResultFileThresholdRows = threshold,
            ResultsDirectory = _dir
        }), NullLogger<SqlServerService>.Instance);

    private static DataTableReader MakeReader(int rows, int textLength = 5)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        for (var i = 1; i <= rows; i++)
            table.Rows.Add(i, new string('x', textLength));
        return table.CreateDataReader();
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ───────────────────────────────────────────────
    // FormatResultAsync: inline vs file
    // ───────────────────────────────────────────────

    [Fact]
    public async Task UnderThreshold_ReturnsInlineJson()
    {
        var root = Parse(await MakeService(threshold: 10)
            .FormatResultAsync(MakeReader(5), toFile: false, TestContext.Current.CancellationToken));

        Assert.False(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(5, root.GetProperty("rows").GetArrayLength());
        Assert.False(root.TryGetProperty("file", out _));
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public async Task OverRowThreshold_WritesCsvWithHeaderAndPreview()
    {
        var root = Parse(await MakeService(threshold: 10)
            .FormatResultAsync(MakeReader(25), toFile: false, TestContext.Current.CancellationToken));

        var file = root.GetProperty("file").GetString()!;
        Assert.StartsWith(_dir, file);
        Assert.Equal(25, root.GetProperty("rowCount").GetInt32());
        Assert.False(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(SqlServerService.PreviewRows, root.GetProperty("preview").GetArrayLength());
        Assert.Equal("Id", root.GetProperty("columns")[0].GetProperty("name").GetString());

        var lines = File.ReadAllLines(file);
        Assert.Equal(26, lines.Length);
        Assert.Equal("Id,Name", lines[0]);
        Assert.Equal("25,xxxxx", lines[^1]);
    }

    [Fact]
    public async Task ToFile_SmallResult_WritesCsv()
    {
        var root = Parse(await MakeService()
            .FormatResultAsync(MakeReader(3), toFile: true, TestContext.Current.CancellationToken));

        Assert.Equal(3, root.GetProperty("rowCount").GetInt32());
        Assert.Equal(4, File.ReadAllLines(root.GetProperty("file").GetString()!).Length);
    }

    [Fact]
    public async Task ThresholdZero_NeverSpills()
    {
        var root = Parse(await MakeService(threshold: 0)
            .FormatResultAsync(MakeReader(50, textLength: 2_000), toFile: false, TestContext.Current.CancellationToken));

        Assert.Equal(50, root.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task MaxRows_CapsFileOutput()
    {
        var root = Parse(await MakeService(maxRows: 20, threshold: 5)
            .FormatResultAsync(MakeReader(30), toFile: false, TestContext.Current.CancellationToken));

        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(20, root.GetProperty("rowCount").GetInt32());
        Assert.Equal(21, File.ReadAllLines(root.GetProperty("file").GetString()!).Length);
    }

    [Fact]
    public async Task WideRows_SpillOnCharLimit_AndPreviewStringsAreShortened()
    {
        var root = Parse(await MakeService()
            .FormatResultAsync(MakeReader(3, textLength: 20_000), toFile: false, TestContext.Current.CancellationToken));

        Assert.Equal(3, root.GetProperty("rowCount").GetInt32());
        var previewName = root.GetProperty("preview")[0].GetProperty("Name").GetString()!;
        Assert.Equal(SqlServerService.PreviewMaxStringLength + 1, previewName.Length);
        Assert.EndsWith("…", previewName);

        // The file keeps the full value
        Assert.Contains(new string('x', 20_000), File.ReadAllText(root.GetProperty("file").GetString()!));
    }

    // ───────────────────────────────────────────────
    // WriteRow
    // ───────────────────────────────────────────────

    [Fact]
    public void WriteRow_QuotesOnlyWhenNeeded_AndUsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var writer = new StringWriter();
            ResultFiles.WriteRow(writer, [null, "plain", "a,b", "say \"hi\"", "line1\nline2", 1.5m, 2.25d]);

            Assert.Equal(",plain,\"a,b\",\"say \"\"hi\"\"\",\"line1\nline2\",1.5,2.25\r\n", writer.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // ───────────────────────────────────────────────
    // DeleteExpired
    // ───────────────────────────────────────────────

    [Fact]
    public void DeleteExpired_RemovesOnlyOldResultFiles()
    {
        Directory.CreateDirectory(_dir);
        var oldResult = Path.Combine(_dir, ResultFiles.FilePrefix + "old.csv");
        var newResult = Path.Combine(_dir, ResultFiles.FilePrefix + "new.csv");
        var oldOther = Path.Combine(_dir, "someone-elses.csv");
        foreach (var f in new[] { oldResult, newResult, oldOther })
            File.WriteAllText(f, "x");
        File.SetLastWriteTimeUtc(oldResult, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldOther, DateTime.UtcNow.AddDays(-10));

        var deleted = ResultFiles.DeleteExpired(_dir, TimeSpan.FromDays(7));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldResult));
        Assert.True(File.Exists(newResult));
        Assert.True(File.Exists(oldOther));
    }

    [Fact]
    public void DeleteExpired_MissingDirectory_ReturnsZero()
    {
        Assert.Equal(0, ResultFiles.DeleteExpired(_dir, TimeSpan.FromDays(7)));
    }
}
