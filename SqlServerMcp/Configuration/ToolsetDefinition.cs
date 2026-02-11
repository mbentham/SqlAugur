namespace SqlServerMcp.Configuration;

internal sealed record ToolsetDefinition(
    string Name,
    string Description,
    Func<SqlServerMcpOptions, bool> IsConfigured,
    Type[] ToolTypes);
