using SqlAugur.Tools;

namespace SqlAugur.Configuration;

internal static class ToolRegistry
{
    internal static readonly Type[] CoreTools =
    [
        typeof(ListServersTool),
        typeof(ListDatabasesTool),
        typeof(ReadDataTool),
        typeof(QueryPlanTool),
        typeof(GetSchemaOverviewTool),
        typeof(DescribeTableTool),
    ];

    internal static readonly Type[] SchemaExplorationTools =
    [
        typeof(ListProgrammableObjectsTool),
        typeof(GetObjectDefinitionTool),
        typeof(ExtendedPropertiesTool),
        typeof(GetObjectDependenciesTool),
    ];

    internal static readonly Type[] DiagramTools =
    [
        typeof(GetPlantUMLDiagramTool),
        typeof(GetMermaidDiagramTool),
    ];

    internal static readonly Type[] FirstResponderKitTools =
    [
        typeof(BlitzTool),
        typeof(BlitzFirstTool),
        typeof(BlitzCacheTool),
        typeof(BlitzIndexTool),
        typeof(BlitzWhoTool),
        typeof(BlitzLockTool),
        typeof(BlitzPlanCompareTool),
    ];

    internal static readonly Type[] DarlingDataTools =
    [
        typeof(PressureDetectorTool),
        typeof(QuickieStoreTool),
        typeof(HealthParserTool),
        typeof(LogHunterTool),
        typeof(HumanEventsBlockViewerTool),
        typeof(IndexCleanupTool),
        typeof(QueryReproBuilderTool),
    ];

    internal static readonly Type[] WhoIsActiveTools =
    [
        typeof(WhoIsActiveTool),
    ];

    internal static readonly Type[] DiscoveryToolTypes =
    [
        typeof(DiscoveryTools),
    ];

    internal static IEnumerable<Type> GetToolTypes(
        bool enableFirstResponderKit, bool enableDarlingData, bool enableWhoIsActive,
        bool enableDynamicToolsets = false)
    {
        if (enableDynamicToolsets)
            return CoreTools.Concat(DiscoveryToolTypes);

        IEnumerable<Type> tools = CoreTools
            .Concat(SchemaExplorationTools)
            .Concat(DiagramTools);

        if (enableFirstResponderKit)
            tools = tools.Concat(FirstResponderKitTools);
        if (enableDarlingData)
            tools = tools.Concat(DarlingDataTools);
        if (enableWhoIsActive)
            tools = tools.Concat(WhoIsActiveTools);

        return tools;
    }
}
