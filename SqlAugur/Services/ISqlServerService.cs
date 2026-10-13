namespace SqlAugur.Services;

public interface ISqlServerService
{
    IReadOnlyList<string> GetServerNames();
    Task<string> ExecuteQueryAsync(string serverName, string databaseName, string query, bool toFile, CancellationToken cancellationToken);
    Task<string> ListDatabasesAsync(string serverName, CancellationToken cancellationToken);
    Task<string> GetEstimatedPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken);
    Task<string> GetActualPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken);
}
