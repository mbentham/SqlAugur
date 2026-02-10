namespace SqlServerMcp.Configuration;

public static class SqlServerMcpOptionsExtensions
{
    public static SqlServerConnection ResolveServer(this SqlServerMcpOptions options, string serverName)
    {
        if (!options.Servers.TryGetValue(serverName, out var serverConfig))
        {
            var available = string.Join(", ", options.Servers.Keys.OrderBy(k => k));
            throw new ArgumentException(
                $"Server '{serverName}' not found. Available servers: {available}");
        }
        return serverConfig;
    }
}
