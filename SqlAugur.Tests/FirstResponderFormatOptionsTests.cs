using SqlAugur.Services;

namespace SqlAugur.Tests;

public class FirstResponderFormatOptionsTests
{
    // ───────────────────────────────────────────────
    // BlitzCache
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzCache_Default_ExcludesQueryPlanColumns()
    {
        var options = FirstResponderService.BuildBlitzCacheOptions(null, null);

        Assert.Contains("QueryPlan", options.ExcludedColumns);
        Assert.Contains("Query Plan", options.ExcludedColumns);
        Assert.Contains("implicit_conversion_info", options.ExcludedColumns);
        Assert.Contains("PlanHandle", options.ExcludedColumns);
        Assert.Contains("SqlHandle", options.ExcludedColumns);
        Assert.Contains("ai_prompt", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzCache_Default_TruncatesQueryText()
    {
        var options = FirstResponderService.BuildBlitzCacheOptions(null, null);

        Assert.Equal(500, options.TruncatedColumns["QueryText"]);
        Assert.Equal(500, options.TruncatedColumns["Query Text"]);
        Assert.Equal(2000, options.TruncatedColumns["Warnings"]);
    }

    [Fact]
    public void BlitzCache_IncludeQueryPlans_KeepsPlanColumns()
    {
        var options = FirstResponderService.BuildBlitzCacheOptions(includeQueryPlans: true, verbose: null);

        Assert.DoesNotContain("QueryPlan", options.ExcludedColumns);
        Assert.DoesNotContain("Query Plan", options.ExcludedColumns);
        // Other non-plan columns still excluded
        Assert.Contains("PlanHandle", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzCache_Verbose_ReturnsEmptyExclusions()
    {
        var options = FirstResponderService.BuildBlitzCacheOptions(null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Empty(options.TruncatedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // BlitzFirst
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzFirst_Default_ExcludesExpectedColumns()
    {
        var options = FirstResponderService.BuildBlitzFirstOptions(null, null);

        Assert.Contains("QueryPlan", options.ExcludedColumns);
        Assert.Contains("PlanHandle", options.ExcludedColumns);
        Assert.Contains("QueryStatsNowID", options.ExcludedColumns);
        Assert.Contains("QueryStatsFirstID", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzFirst_Default_TruncatesCorrectly()
    {
        var options = FirstResponderService.BuildBlitzFirstOptions(null, null);

        Assert.Equal(500, options.TruncatedColumns["QueryText"]);
        Assert.Equal(1000, options.TruncatedColumns["HowToStopIt"]);
    }

    [Fact]
    public void BlitzFirst_IncludeQueryPlans_KeepsQueryPlan()
    {
        var options = FirstResponderService.BuildBlitzFirstOptions(includeQueryPlans: true, verbose: null);

        Assert.DoesNotContain("QueryPlan", options.ExcludedColumns);
        Assert.Contains("PlanHandle", options.ExcludedColumns);
    }

    // ───────────────────────────────────────────────
    // BlitzWho
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzWho_Default_ExcludesExpectedColumns()
    {
        var options = FirstResponderService.BuildBlitzWhoOptions(null, null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        Assert.Contains("live_query_plan", options.ExcludedColumns);
        Assert.Contains("cached_parameter_info", options.ExcludedColumns);
        Assert.Contains("sql_handle", options.ExcludedColumns);
        Assert.Contains("outer_command", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzWho_Default_TruncatesCorrectly()
    {
        var options = FirstResponderService.BuildBlitzWhoOptions(null, null);

        Assert.Equal(500, options.TruncatedColumns["query_text"]);
        Assert.Equal(500, options.TruncatedColumns["top_session_waits"]);
    }

    [Fact]
    public void BlitzWho_IncludeQueryPlans_KeepsPlanColumns()
    {
        var options = FirstResponderService.BuildBlitzWhoOptions(includeQueryPlans: true, verbose: null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
        Assert.DoesNotContain("live_query_plan", options.ExcludedColumns);
        Assert.Contains("sql_handle", options.ExcludedColumns);
    }

    // ───────────────────────────────────────────────
    // Blitz
    // ───────────────────────────────────────────────

    [Fact]
    public void Blitz_Default_ExcludesExpectedColumns()
    {
        var options = FirstResponderService.BuildBlitzOptions(null, null);

        Assert.Contains("QueryPlan", options.ExcludedColumns);
        Assert.Contains("QueryPlanFiltered", options.ExcludedColumns);
        Assert.Equal(2000, options.TruncatedColumns["Details"]);
    }

    [Fact]
    public void Blitz_IncludeQueryPlans_KeepsPlanColumns()
    {
        var options = FirstResponderService.BuildBlitzOptions(includeQueryPlans: true, verbose: null);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(2000, options.TruncatedColumns["Details"]);
    }

    [Fact]
    public void Blitz_Verbose_ReturnsEmptyExclusions()
    {
        var options = FirstResponderService.BuildBlitzOptions(null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Empty(options.TruncatedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // BlitzIndex
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzIndex_Default_ExcludesExpectedColumns()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(null, null);

        Assert.Contains("sample_query_plan", options.ExcludedColumns);
        Assert.Contains("more_info", options.ExcludedColumns);
        Assert.Contains("blitz_result_id", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzIndex_Default_TruncatesCorrectly()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(null, null);

        Assert.Equal(1000, options.TruncatedColumns["create_tsql"]);
        Assert.Equal(2000, options.TruncatedColumns["details"]);
        Assert.Equal(500, options.TruncatedColumns["index_definition"]);
        Assert.Equal(500, options.TruncatedColumns["secret_columns"]);
    }

    [Fact]
    public void BlitzIndex_IncludeQueryPlans_KeepsSampleQueryPlan()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(includeQueryPlans: true, verbose: null);

        Assert.DoesNotContain("sample_query_plan", options.ExcludedColumns);
        Assert.Contains("more_info", options.ExcludedColumns);
    }

    // ───────────────────────────────────────────────
    // BlitzLock
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzLock_Default_ExcludesExpectedColumns()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, null, null);

        Assert.Contains("deadlock_graph", options.ExcludedColumns);
        Assert.Contains("process_xml", options.ExcludedColumns);
        Assert.Contains("query_plan", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzLock_Default_TruncatesCorrectly()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, null, null);

        Assert.Equal(500, options.TruncatedColumns["query"]);
        Assert.Equal(500, options.TruncatedColumns["query_xml"]);
        Assert.Equal(500, options.TruncatedColumns["object_names"]);
        Assert.Equal(2000, options.TruncatedColumns["finding"]);
    }

    [Fact]
    public void BlitzLock_IncludeQueryPlans_KeepsQueryPlan()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(includeQueryPlans: true, null, null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
        Assert.Contains("deadlock_graph", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzLock_IncludeXmlReports_KeepsXmlColumns()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, includeXmlReports: true, null);

        Assert.DoesNotContain("deadlock_graph", options.ExcludedColumns);
        Assert.DoesNotContain("process_xml", options.ExcludedColumns);
        Assert.DoesNotContain("parallel_deadlock_details", options.ExcludedColumns);
        Assert.Contains("query_plan", options.ExcludedColumns);
    }

    [Fact]
    public void BlitzLock_Verbose_ReturnsEmptyExclusions()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // MaxRows
    // ───────────────────────────────────────────────

    [Fact]
    public void BlitzIndex_WithMaxRows_SetsMaxRowsOverride()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(null, null, maxRows: 50);
        Assert.Equal(50, options.MaxRowsOverride);
    }

    [Fact]
    public void BlitzIndex_WithoutMaxRows_MaxRowsOverrideIsNull()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(null, null);
        Assert.Null(options.MaxRowsOverride);
    }

    [Fact]
    public void BlitzIndex_Verbose_WithMaxRows_SetsMaxRowsOverride()
    {
        var options = FirstResponderService.BuildBlitzIndexOptions(null, verbose: true, maxRows: 25);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
        Assert.Equal(25, options.MaxRowsOverride);
    }

    [Fact]
    public void BlitzLock_WithMaxRows_SetsMaxRowsOverride()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, null, null, maxRows: 100);
        Assert.Equal(100, options.MaxRowsOverride);
    }

    [Fact]
    public void BlitzLock_WithoutMaxRows_MaxRowsOverrideIsNull()
    {
        var options = FirstResponderService.BuildBlitzLockOptions(null, null, null);
        Assert.Null(options.MaxRowsOverride);
    }
}
