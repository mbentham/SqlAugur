using SqlAugur.Services;

namespace SqlAugur.Tests;

public class DarlingDataFormatOptionsTests
{
    // ───────────────────────────────────────────────
    // PressureDetector
    // ───────────────────────────────────────────────

    [Fact]
    public void PressureDetector_Default_ExcludesQueryPlan()
    {
        var options = DarlingDataService.BuildPressureDetectorOptions(null, null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        Assert.Contains("live_query_plan", options.ExcludedColumns);
        Assert.Equal(1000, options.TruncatedColumns["sql_text"]);
        Assert.Equal(2000, options.TruncatedColumns["tempdb_info"]);
    }

    [Fact]
    public void PressureDetector_IncludeQueryPlans_KeepsPlanColumns()
    {
        var options = DarlingDataService.BuildPressureDetectorOptions(includeQueryPlans: true, verbose: null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
        Assert.DoesNotContain("live_query_plan", options.ExcludedColumns);
    }

    [Fact]
    public void PressureDetector_Verbose_ReturnsEmptyExclusions()
    {
        var options = DarlingDataService.BuildPressureDetectorOptions(null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // QuickieStore
    // ───────────────────────────────────────────────

    [Fact]
    public void QuickieStore_Default_ExcludesQueryPlanAndMetrics()
    {
        var options = DarlingDataService.BuildQuickieStoreOptions(null, null, null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        // Check some metric columns are excluded
        Assert.Contains("min_duration_ms", options.ExcludedColumns);
        Assert.Contains("max_cpu_time_ms", options.ExcludedColumns);
        Assert.Contains("total_logical_io_reads", options.ExcludedColumns);
        Assert.Equal(1000, options.TruncatedColumns["query_sql_text"]);
    }

    [Fact]
    public void QuickieStore_VerboseMetrics_KeepsMetricColumns()
    {
        var options = DarlingDataService.BuildQuickieStoreOptions(null, verboseMetrics: true, verbose: null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        Assert.DoesNotContain("min_duration_ms", options.ExcludedColumns);
        Assert.DoesNotContain("max_cpu_time_ms", options.ExcludedColumns);
    }

    [Fact]
    public void QuickieStore_IncludeQueryPlans_KeepsQueryPlan()
    {
        var options = DarlingDataService.BuildQuickieStoreOptions(includeQueryPlans: true, null, null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
    }

    [Fact]
    public void QuickieStore_Verbose_ReturnsEmptyExclusions()
    {
        var options = DarlingDataService.BuildQuickieStoreOptions(null, null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // HealthParser
    // ───────────────────────────────────────────────

    [Fact]
    public void HealthParser_Default_ExcludesXmlColumns()
    {
        var options = DarlingDataService.BuildHealthParserOptions(null, null, null);

        Assert.Contains("deadlock_graph", options.ExcludedColumns);
        Assert.Contains("xml_deadlock_report", options.ExcludedColumns);
        Assert.Contains("blocked_process_report", options.ExcludedColumns);
        Assert.Equal(1000, options.TruncatedColumns["query_text"]);
    }

    [Fact]
    public void HealthParser_IncludeXmlReports_KeepsXmlColumns()
    {
        var options = DarlingDataService.BuildHealthParserOptions(null, includeXmlReports: true, null);

        Assert.Empty(options.ExcludedColumns);
    }

    [Fact]
    public void HealthParser_Verbose_ReturnsEmptyExclusions()
    {
        var options = DarlingDataService.BuildHealthParserOptions(null, null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // HumanEventsBlockViewer
    // ───────────────────────────────────────────────

    [Fact]
    public void HumanEventsBlockViewer_Default_ExcludesExpectedColumns()
    {
        var options = DarlingDataService.BuildHumanEventsBlockViewerOptions(null, null, null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        Assert.Contains("blocked_process_report_xml", options.ExcludedColumns);
        Assert.Contains("sql_handle", options.ExcludedColumns);
        Assert.Equal(1000, options.TruncatedColumns["query_text"]);
    }

    [Fact]
    public void HumanEventsBlockViewer_IncludeQueryPlans_KeepsQueryPlan()
    {
        var options = DarlingDataService.BuildHumanEventsBlockViewerOptions(includeQueryPlans: true, null, null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
        Assert.Contains("blocked_process_report_xml", options.ExcludedColumns);
    }

    [Fact]
    public void HumanEventsBlockViewer_IncludeXmlReports_KeepsXml()
    {
        var options = DarlingDataService.BuildHumanEventsBlockViewerOptions(null, includeXmlReports: true, null);

        Assert.DoesNotContain("blocked_process_report_xml", options.ExcludedColumns);
        Assert.Contains("query_plan", options.ExcludedColumns);
    }

    // ───────────────────────────────────────────────
    // LogHunter
    // ───────────────────────────────────────────────

    [Fact]
    public void LogHunter_Default_TruncatesText()
    {
        var options = DarlingDataService.BuildLogHunterOptions(null);

        Assert.Equal(200, options.MaxRowsOverride);
        Assert.Equal(500, options.TruncatedColumns["text"]);
    }

    [Fact]
    public void LogHunter_Verbose_ReturnsUnlimited()
    {
        var options = DarlingDataService.BuildLogHunterOptions(verbose: true);

        Assert.Null(options.MaxRowsOverride);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // IndexCleanup
    // ───────────────────────────────────────────────

    [Fact]
    public void IndexCleanup_Default_TruncatesDefinition()
    {
        var options = DarlingDataService.BuildIndexCleanupOptions(null);

        Assert.Equal(500, options.TruncatedColumns["original_index_definition"]);
    }

    [Fact]
    public void IndexCleanup_Verbose_ReturnsUnlimited()
    {
        var options = DarlingDataService.BuildIndexCleanupOptions(verbose: true);

        Assert.Empty(options.TruncatedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }

    // ───────────────────────────────────────────────
    // QueryReproBuilder
    // ───────────────────────────────────────────────

    [Fact]
    public void QueryReproBuilder_Default_ExcludesQueryPlan()
    {
        var options = DarlingDataService.BuildQueryReproBuilderOptions(null, null);

        Assert.Contains("query_plan", options.ExcludedColumns);
        Assert.Equal(1000, options.TruncatedColumns["query_sql_text"]);
    }

    [Fact]
    public void QueryReproBuilder_IncludeQueryPlans_KeepsQueryPlan()
    {
        var options = DarlingDataService.BuildQueryReproBuilderOptions(includeQueryPlans: true, null);

        Assert.DoesNotContain("query_plan", options.ExcludedColumns);
    }

    [Fact]
    public void QueryReproBuilder_Verbose_ReturnsEmptyExclusions()
    {
        var options = DarlingDataService.BuildQueryReproBuilderOptions(null, verbose: true);

        Assert.Empty(options.ExcludedColumns);
        Assert.Equal(int.MaxValue, options.MaxStringLength);
    }
}
