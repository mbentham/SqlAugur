using SqlAugur.Services;

namespace SqlAugur.Tests;

public class ResultSetFormatOptionsTests
{
    [Fact]
    public void Default_HasEmptyCollections()
    {
        var options = ResultSetFormatOptions.Default;

        Assert.Empty(options.ExcludedColumns);
        Assert.Empty(options.TruncatedColumns);
        Assert.Empty(options.ExcludedResultSets);
        Assert.Null(options.MaxRowsOverride);
        Assert.Null(options.MaxStringLength);
    }

    [Fact]
    public void ExcludedColumns_IsCaseInsensitive()
    {
        var options = new ResultSetFormatOptions
        {
            ExcludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QueryPlan" }
        };

        Assert.Contains("queryplan", options.ExcludedColumns);
        Assert.Contains("QUERYPLAN", options.ExcludedColumns);
        Assert.Contains("QueryPlan", options.ExcludedColumns);
    }

    [Fact]
    public void TruncatedColumns_IsCaseInsensitive()
    {
        var options = new ResultSetFormatOptions
        {
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["QueryText"] = 500
            }
        };

        Assert.True(options.TruncatedColumns.ContainsKey("querytext"));
        Assert.True(options.TruncatedColumns.ContainsKey("QUERYTEXT"));
        Assert.Equal(500, options.TruncatedColumns["QueryText"]);
    }

    [Fact]
    public void InitSyntax_SetsValuesCorrectly()
    {
        var options = new ResultSetFormatOptions
        {
            ExcludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ColA", "ColB" },
            TruncatedColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ColC"] = 1000
            },
            MaxRowsOverride = 50,
            ExcludedResultSets = [0, 2],
            MaxStringLength = 4000
        };

        Assert.Equal(2, options.ExcludedColumns.Count);
        Assert.Single(options.TruncatedColumns);
        Assert.Equal(50, options.MaxRowsOverride);
        Assert.Contains(0, options.ExcludedResultSets);
        Assert.Contains(2, options.ExcludedResultSets);
        Assert.Equal(4000, options.MaxStringLength);
    }
}
