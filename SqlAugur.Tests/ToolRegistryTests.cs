using SqlAugur.Configuration;
using SqlAugur.Tools;

namespace SqlAugur.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void CoreTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(ListServersTool), ToolRegistry.CoreTools);
        Assert.Contains(typeof(ListDatabasesTool), ToolRegistry.CoreTools);
        Assert.Contains(typeof(ReadDataTool), ToolRegistry.CoreTools);
        Assert.Contains(typeof(QueryPlanTool), ToolRegistry.CoreTools);
        Assert.Contains(typeof(GetSchemaOverviewTool), ToolRegistry.CoreTools);
        Assert.Contains(typeof(DescribeTableTool), ToolRegistry.CoreTools);
    }

    [Fact]
    public void CoreTools_HasExactCount()
    {
        Assert.Equal(6, ToolRegistry.CoreTools.Length);
    }

    [Fact]
    public void SchemaExplorationTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(ListProgrammableObjectsTool), ToolRegistry.SchemaExplorationTools);
        Assert.Contains(typeof(GetObjectDefinitionTool), ToolRegistry.SchemaExplorationTools);
        Assert.Contains(typeof(ExtendedPropertiesTool), ToolRegistry.SchemaExplorationTools);
        Assert.Contains(typeof(GetObjectDependenciesTool), ToolRegistry.SchemaExplorationTools);
    }

    [Fact]
    public void SchemaExplorationTools_HasExactCount()
    {
        Assert.Equal(4, ToolRegistry.SchemaExplorationTools.Length);
    }

    [Fact]
    public void DiagramTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(GetPlantUMLDiagramTool), ToolRegistry.DiagramTools);
        Assert.Contains(typeof(GetMermaidDiagramTool), ToolRegistry.DiagramTools);
    }

    [Fact]
    public void DiagramTools_HasExactCount()
    {
        Assert.Equal(2, ToolRegistry.DiagramTools.Length);
    }

    [Fact]
    public void CoreTools_DoesNotContainDiagramTool()
    {
        Assert.DoesNotContain(typeof(GetPlantUMLDiagramTool), ToolRegistry.CoreTools);
    }

    [Fact]
    public void FirstResponderKitTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(BlitzTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzFirstTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzCacheTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzIndexTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzWhoTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzLockTool), ToolRegistry.FirstResponderKitTools);
        Assert.Contains(typeof(BlitzPlanCompareTool), ToolRegistry.FirstResponderKitTools);
    }

    [Fact]
    public void FirstResponderKitTools_HasExactCount()
    {
        Assert.Equal(7, ToolRegistry.FirstResponderKitTools.Length);
    }

    [Fact]
    public void DarlingDataTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(PressureDetectorTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(QuickieStoreTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(HealthParserTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(LogHunterTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(HumanEventsBlockViewerTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(IndexCleanupTool), ToolRegistry.DarlingDataTools);
        Assert.Contains(typeof(QueryReproBuilderTool), ToolRegistry.DarlingDataTools);
    }

    [Fact]
    public void DarlingDataTools_HasExactCount()
    {
        Assert.Equal(7, ToolRegistry.DarlingDataTools.Length);
    }

    [Fact]
    public void WhoIsActiveTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(WhoIsActiveTool), ToolRegistry.WhoIsActiveTools);
    }

    [Fact]
    public void WhoIsActiveTools_HasExactCount()
    {
        Assert.Single(ToolRegistry.WhoIsActiveTools);
    }

    [Fact]
    public void DiscoveryTools_ContainsExpectedTypes()
    {
        Assert.Contains(typeof(DiscoveryTools), ToolRegistry.DiscoveryToolTypes);
    }

    [Fact]
    public void DiscoveryTools_HasExactCount()
    {
        Assert.Single(ToolRegistry.DiscoveryToolTypes);
    }

    [Fact]
    public void AllArrays_HaveNoOverlap()
    {
        var arrays = new[]
        {
            ToolRegistry.CoreTools,
            ToolRegistry.SchemaExplorationTools,
            ToolRegistry.DiagramTools,
            ToolRegistry.FirstResponderKitTools,
            ToolRegistry.DarlingDataTools,
            ToolRegistry.WhoIsActiveTools,
            ToolRegistry.DiscoveryToolTypes,
        };

        for (var i = 0; i < arrays.Length; i++)
        {
            for (var j = i + 1; j < arrays.Length; j++)
            {
                var overlap = arrays[i].Intersect(arrays[j]);
                Assert.Empty(overlap);
            }
        }
    }

    [Fact]
    public void AllRegisteredTools_HaveNoDuplicates()
    {
        var all = ToolRegistry.CoreTools
            .Concat(ToolRegistry.SchemaExplorationTools)
            .Concat(ToolRegistry.DiagramTools)
            .Concat(ToolRegistry.FirstResponderKitTools)
            .Concat(ToolRegistry.DarlingDataTools)
            .Concat(ToolRegistry.WhoIsActiveTools)
            .Concat(ToolRegistry.DiscoveryToolTypes)
            .ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    // ───────────────────────────────────────────────
    // Static mode (enableDynamicToolsets=false)
    // ───────────────────────────────────────────────

    [Fact]
    public void GetToolTypes_AllDisabled_ReturnsCoreAndAlwaysAvailable()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: false, enableDarlingData: false, enableWhoIsActive: false).ToList();

        // 6 core + 4 schema exploration + 2 diagram = 12
        Assert.Equal(12, types.Count);
        Assert.Contains(typeof(ListServersTool), types);
        Assert.Contains(typeof(QueryPlanTool), types);
        Assert.Contains(typeof(GetPlantUMLDiagramTool), types);
        Assert.DoesNotContain(typeof(BlitzTool), types);
        Assert.DoesNotContain(typeof(PressureDetectorTool), types);
        Assert.DoesNotContain(typeof(WhoIsActiveTool), types);
    }

    [Fact]
    public void GetToolTypes_FirstResponderKitOnly_Returns19()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: false, enableWhoIsActive: false).ToList();

        Assert.Equal(19, types.Count);
        Assert.Contains(typeof(BlitzTool), types);
        Assert.DoesNotContain(typeof(PressureDetectorTool), types);
        Assert.DoesNotContain(typeof(WhoIsActiveTool), types);
    }

    [Fact]
    public void GetToolTypes_DarlingDataOnly_Returns19()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: false, enableDarlingData: true, enableWhoIsActive: false).ToList();

        Assert.Equal(19, types.Count);
        Assert.DoesNotContain(typeof(BlitzTool), types);
        Assert.Contains(typeof(PressureDetectorTool), types);
        Assert.DoesNotContain(typeof(WhoIsActiveTool), types);
    }

    [Fact]
    public void GetToolTypes_WhoIsActiveOnly_Returns13()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: false, enableDarlingData: false, enableWhoIsActive: true).ToList();

        Assert.Equal(13, types.Count);
        Assert.DoesNotContain(typeof(BlitzTool), types);
        Assert.DoesNotContain(typeof(PressureDetectorTool), types);
        Assert.Contains(typeof(WhoIsActiveTool), types);
    }

    [Fact]
    public void GetToolTypes_AllEnabled_ReturnsAllTools()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: true, enableWhoIsActive: true).ToList();

        Assert.Equal(27, types.Count);
    }

    // ───────────────────────────────────────────────
    // Dynamic toolset mode
    // ───────────────────────────────────────────────

    [Fact]
    public void GetToolTypes_DynamicMode_ReturnsCoreAndDiscovery()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: true, enableWhoIsActive: true,
            enableDynamicToolsets: true).ToList();

        // 6 core + 1 discovery type
        Assert.Equal(7, types.Count);
        Assert.Contains(typeof(ListServersTool), types);
        Assert.Contains(typeof(DiscoveryTools), types);
    }

    [Fact]
    public void GetToolTypes_DynamicMode_ExcludesDbaAndDiagramTools()
    {
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: true, enableWhoIsActive: true,
            enableDynamicToolsets: true).ToList();

        Assert.DoesNotContain(typeof(BlitzTool), types);
        Assert.DoesNotContain(typeof(PressureDetectorTool), types);
        Assert.DoesNotContain(typeof(WhoIsActiveTool), types);
        Assert.DoesNotContain(typeof(GetPlantUMLDiagramTool), types);
    }

    [Fact]
    public void GetToolTypes_DynamicMode_IgnoresIndividualFlags()
    {
        // Even with only FRK enabled, dynamic mode still just returns core + discovery
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: false, enableWhoIsActive: false,
            enableDynamicToolsets: true).ToList();

        Assert.Equal(7, types.Count);
        Assert.DoesNotContain(typeof(BlitzTool), types);
        Assert.Contains(typeof(DiscoveryTools), types);
    }

    [Fact]
    public void GetToolTypes_DynamicMode_AllDbaFlagsOff_StillIncludesDiscovery()
    {
        // Discovery tools always load in dynamic mode — schema_exploration and diagrams are always available
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: false, enableDarlingData: false, enableWhoIsActive: false,
            enableDynamicToolsets: true).ToList();

        Assert.Equal(7, types.Count);
        Assert.Contains(typeof(DiscoveryTools), types);
    }

    [Fact]
    public void GetToolTypes_DynamicModeFalse_DefaultParameter_ExistingBehavior()
    {
        // Verify the default parameter value preserves existing behavior
        var types = ToolRegistry.GetToolTypes(
            enableFirstResponderKit: true, enableDarlingData: false, enableWhoIsActive: false).ToList();

        Assert.Equal(19, types.Count);
        Assert.Contains(typeof(BlitzTool), types);
        Assert.DoesNotContain(typeof(DiscoveryTools), types);
    }
}
