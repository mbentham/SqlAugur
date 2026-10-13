using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SqlAugur.Services;

namespace SqlAugur.Tools;

[McpServerToolType]
public sealed class ReadDataTool
{
    private readonly ISqlServerService _sqlServerService;
    private readonly IRateLimitingService _rateLimiter;

    public ReadDataTool(ISqlServerService sqlServerService, IRateLimitingService rateLimiter)
    {
        _sqlServerService = sqlServerService;
        _rateLimiter = rateLimiter;
    }

    // Still annotated read-only (unlike get_query_plan): result files are a server-managed cache
    // with unique names in ResultsDirectory, never a caller-chosen path.
    [McpServerTool(
        Name = "read_data",
        Title = "Read Data from SQL Server",
        ReadOnly = true,
        Idempotent = true)]
    [Description("Execute a read-only SQL SELECT query against a named SQL Server instance. Only SELECT and WITH (CTE) queries are allowed. Use list_servers first to discover available server names. " +
        "Small results are returned inline as JSON {truncated, rows}. Large results (over the configured row threshold, 500 by default, or about 40,000 characters) " +
        "are written to a CSV file instead, and the response gives the file path, row count, column names and types, and a 10-row preview. " +
        "Process a result file with code (e.g. Python csv/pandas, PowerShell Import-Csv) rather than reading it into context.")]
    public async Task<string> ReadData(
        [Description("Name of the SQL Server to query (use list_servers to see available names)")] string serverName,
        [Description("SQL SELECT query to execute. Only SELECT and WITH (CTE) queries are permitted.")] string query,
        [Description("Name of the database to query (use list_databases to see available databases)")] string databaseName,
        [Description("Write the full result to a CSV file and return its path and a preview, whatever its size. Use when the rows will be processed with code.")] bool toFile = false,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelper.ExecuteAsync(_rateLimiter, () =>
            _sqlServerService.ExecuteQueryAsync(serverName, databaseName, query, toFile, cancellationToken), cancellationToken);
    }
}
