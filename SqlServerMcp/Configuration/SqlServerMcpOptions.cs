namespace SqlServerMcp.Configuration;

public sealed class SqlServerMcpOptions
{
    public Dictionary<string, SqlServerConnection> Servers { get; init; } = new();
    public int MaxRows { get; init; } = 1000;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public bool EnableDbaTools { get; init; }
}
