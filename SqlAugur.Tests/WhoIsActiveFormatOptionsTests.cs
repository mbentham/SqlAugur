using SqlAugur.Services;

namespace SqlAugur.Tests;

public class WhoIsActiveFormatOptionsTests
{
    [Fact]
    public void DefaultOutputColumnList_IncludesSessionId()
    {
        Assert.Contains("[session_id]", WhoIsActiveService.DefaultOutputColumnList);
    }

    [Fact]
    public void DefaultOutputColumnList_ExcludesQueryPlan()
    {
        Assert.DoesNotContain("[query_plan]", WhoIsActiveService.DefaultOutputColumnList);
    }

    [Fact]
    public void DefaultOutputColumnList_ExcludesLocks()
    {
        Assert.DoesNotContain("[locks]", WhoIsActiveService.DefaultOutputColumnList);
    }

    [Fact]
    public void DefaultOutputColumnList_ExcludesAdditionalInfo()
    {
        Assert.DoesNotContain("[additional_info]", WhoIsActiveService.DefaultOutputColumnList);
    }

    [Fact]
    public void CompactOutputColumnList_IsSubsetOfDefault()
    {
        // Compact list should be smaller
        Assert.True(WhoIsActiveService.CompactOutputColumnList.Length <
                     WhoIsActiveService.DefaultOutputColumnList.Length);
        Assert.Contains("[session_id]", WhoIsActiveService.CompactOutputColumnList);
    }

    [Fact]
    public void CompactOutputColumnList_ExcludesLoginName()
    {
        Assert.DoesNotContain("[login_name]", WhoIsActiveService.CompactOutputColumnList);
    }

    // ───────────────────────────────────────────────
    // BuildWhoIsActiveOptions
    // ───────────────────────────────────────────────

    [Fact]
    public void Default_TruncatesSqlTextAt4000()
    {
        var options = WhoIsActiveService.BuildWhoIsActiveOptions(null, null);

        Assert.Equal(4000, options.TruncatedColumns["sql_text"]);
        Assert.Equal(4000, options.TruncatedColumns["sql_command"]);
        Assert.Equal(500, options.TruncatedColumns["query_plan"]);
        Assert.Equal(2000, options.TruncatedColumns["locks"]);
        Assert.Equal(2000, options.TruncatedColumns["additional_info"]);
        Assert.Equal(1000, options.TruncatedColumns["memory_info"]);
    }

    [Fact]
    public void Compact_SetsMaxStringLength500()
    {
        var options = WhoIsActiveService.BuildWhoIsActiveOptions(compact: true, verbose: null);

        Assert.Equal(500, options.MaxStringLength);
        Assert.Empty(options.TruncatedColumns);
    }

    [Fact]
    public void Verbose_DisablesAllTruncation()
    {
        var options = WhoIsActiveService.BuildWhoIsActiveOptions(null, verbose: true);

        Assert.Empty(options.TruncatedColumns);
        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }
}
