namespace SqlServerMcp.Configuration;

public sealed class SqlServerMcpOptions
{
    public Dictionary<string, SqlServerConnection> Servers { get; init; } = new();
    public int MaxRows { get; init; } = 1000;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int MaxConcurrentQueries { get; init; } = 5;
    public int MaxQueriesPerMinute { get; init; } = 60;
    public bool EnableFirstResponderKit { get; init; }
    public bool EnableDarlingData { get; init; }
    public bool EnableWhoIsActive { get; init; }
    public bool EnableDynamicToolsets { get; init; }
}
