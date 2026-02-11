using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

internal sealed class ToolsetManager : IToolsetManager
{
    internal static readonly FrozenDictionary<string, ToolsetDefinition> Toolsets = new Dictionary<string, ToolsetDefinition>
    {
        ["first_responder_kit"] = new(
            "first_responder_kit",
            "Brent Ozar's First Responder Kit — sp_Blitz, sp_BlitzFirst, sp_BlitzCache, sp_BlitzIndex, sp_BlitzWho, sp_BlitzLock for SQL Server health checks, performance analysis, and diagnostics.",
            opts => opts.EnableFirstResponderKit,
            ToolRegistry.FirstResponderKitTools),
        ["darling_data"] = new(
            "darling_data",
            "Erik Darling's diagnostic toolkit — sp_PressureDetector, sp_QuickieStore, sp_HealthParser, sp_LogHunter, sp_HumanEventsBlockViewer, sp_IndexCleanup, sp_QueryReproBuilder for pressure analysis, query store insights, and blocking investigation.",
            opts => opts.EnableDarlingData,
            ToolRegistry.DarlingDataTools),
        ["whoisactive"] = new(
            "whoisactive",
            "Adam Mechanic's sp_WhoIsActive — real-time monitoring of active sessions, running queries, blocking chains, and resource consumption.",
            opts => opts.EnableWhoIsActive,
            ToolRegistry.WhoIsActiveTools),
    }.ToFrozenDictionary();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly McpServerOptions _mcpOptions;
    private readonly SqlServerMcpOptions _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolsetManager> _logger;
    private readonly HashSet<string> _enabledToolsets = new();
    private readonly object _lock = new();

    public ToolsetManager(
        IOptions<McpServerOptions> mcpOptions,
        IOptions<SqlServerMcpOptions> config,
        IServiceProvider serviceProvider,
        ILogger<ToolsetManager> logger)
    {
        _mcpOptions = mcpOptions.Value;
        _config = config.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string GetToolsetSummaries()
    {
        var summaries = new List<object>();

        foreach (var (name, definition) in Toolsets)
        {
            string status;
            lock (_lock)
            {
                if (_enabledToolsets.Contains(name))
                    status = "enabled";
                else if (definition.IsConfigured(_config))
                    status = "available";
                else
                    status = "not_configured";
            }

            var toolNames = EnumerateToolMethods(definition.ToolTypes)
                .Select(t => t.Attr.Name ?? t.Method.Name)
                .ToList();

            summaries.Add(new
            {
                name,
                description = definition.Description,
                status,
                toolCount = toolNames.Count,
                toolNames,
            });
        }

        return JsonSerializer.Serialize(summaries, JsonOptions);
    }

    public string GetToolsetDetails(string toolsetName)
    {
        if (!Toolsets.TryGetValue(toolsetName, out var definition))
            throw new ArgumentException($"Unknown toolset '{toolsetName}'. Valid names: {string.Join(", ", Toolsets.Keys)}");

        var tools = new List<object>();

        foreach (var (toolType, method, toolAttr) in EnumerateToolMethods(definition.ToolTypes))
        {
            var descAttr = method.GetCustomAttribute<DescriptionAttribute>();

            var parameters = new List<object>();
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType == typeof(CancellationToken))
                    continue;
                if (IsServiceParameter(param))
                    continue;

                var paramDesc = param.GetCustomAttribute<DescriptionAttribute>();
                parameters.Add(new
                {
                    name = param.Name,
                    type = GetFriendlyTypeName(param.ParameterType),
                    description = paramDesc?.Description,
                    optional = param.HasDefaultValue,
                });
            }

            tools.Add(new
            {
                name = toolAttr.Name ?? method.Name,
                title = toolAttr.Title,
                description = descAttr?.Description,
                parameters,
            });
        }

        return JsonSerializer.Serialize(new
        {
            toolset = toolsetName,
            description = definition.Description,
            configured = definition.IsConfigured(_config),
            tools,
        }, JsonOptions);
    }

    public string EnableToolset(string toolsetName)
    {
        if (!Toolsets.TryGetValue(toolsetName, out var definition))
            throw new ArgumentException($"Unknown toolset '{toolsetName}'. Valid names: {string.Join(", ", Toolsets.Keys)}");

        if (!definition.IsConfigured(_config))
            throw new McpException($"Toolset '{toolsetName}' is not configured. The server administrator must enable it in appsettings.json.");

        lock (_lock)
        {
            if (!_enabledToolsets.Add(toolsetName))
                return JsonSerializer.Serialize(new { toolset = toolsetName, status = "already_enabled", message = $"Toolset '{toolsetName}' is already enabled." });

            var addedTools = new List<string>();
            var createOptions = new McpServerToolCreateOptions { Services = _serviceProvider };

            foreach (var (toolType, method, toolAttr) in EnumerateToolMethods(definition.ToolTypes))
            {
                McpServerTool tool;
                if (method.IsStatic)
                {
                    tool = McpServerTool.Create(method, target: null, createOptions);
                }
                else
                {
                    tool = McpServerTool.Create(
                        method,
                        _ => ActivatorUtilities.CreateInstance(_serviceProvider, toolType),
                        createOptions);
                }

                (_mcpOptions.ToolCollection ??= []).Add(tool);
                addedTools.Add(toolAttr.Name ?? method.Name);
            }

            _logger.LogInformation("Toolset '{Toolset}' enabled with {Count} tools: {Tools}",
                toolsetName, addedTools.Count, string.Join(", ", addedTools));

            return JsonSerializer.Serialize(new
            {
                toolset = toolsetName,
                status = "enabled",
                message = $"Toolset '{toolsetName}' is now enabled with {addedTools.Count} tools.",
                tools = addedTools,
            });
        }
    }

    private static IEnumerable<(Type ToolType, MethodInfo Method, McpServerToolAttribute Attr)> EnumerateToolMethods(Type[] toolTypes)
    {
        foreach (var toolType in toolTypes)
        {
            foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr is not null)
                    yield return (toolType, method, toolAttr);
            }
        }
    }

    private static bool IsServiceParameter(ParameterInfo param)
    {
        return param.ParameterType.IsInterface
            && param.ParameterType.Namespace?.StartsWith("SqlServerMcp") == true;
    }

    internal static string GetFriendlyTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return GetFriendlyTypeName(underlying) + "?";

        return type.Name switch
        {
            "String" => "string",
            "Boolean" => "bool",
            "Int32" => "int",
            "Int64" => "long",
            "Double" => "double",
            _ => type.Name,
        };
    }
}
