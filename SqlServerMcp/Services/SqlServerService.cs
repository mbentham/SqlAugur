using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

public sealed class SqlServerService : ISqlServerService
{
    private readonly SqlServerMcpOptions _options;
    private readonly ILogger<SqlServerService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public SqlServerService(
        IOptions<SqlServerMcpOptions> options,
        ILogger<SqlServerService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> GetServerNames()
    {
        return _options.Servers.Keys.OrderBy(k => k).ToList();
    }

    public async Task<string> ExecuteQueryAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
    {
        // Validate server name
        var serverConfig = _options.ResolveServer(serverName);

        // Validate query
        var validationError = QueryValidator.Validate(query);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        _logger.LogInformation("Executing query on server {Server} database {Database} ({QueryLength} chars)", serverName, databaseName, query.Length);
        _logger.LogDebug("Query text: {Query}", query);

        // Execute query
        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        await using var command = new SqlCommand(query, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Read rows up to MaxRows
        var rows = new List<Dictionary<string, object?>>();
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= _options.MaxRows)
            {
                truncated = true;
                break;
            }

            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : FormatValue(reader.GetValue(i));
                row[name] = value;
            }
            rows.Add(row);
        }

        // Build response envelope
        var response = new Dictionary<string, object?>
        {
            ["truncated"] = truncated,
            ["rows"] = rows
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    public async Task<string> ListDatabasesAsync(string serverName, CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Listing databases on server {Server}", serverName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT name FROM sys.databases ORDER BY name",
            connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var databases = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            databases.Add(reader.GetString(0));
        }

        return string.Join(", ", databases);
    }

    public Task<string> GetEstimatedPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken) =>
        GetPlanCoreAsync(serverName, databaseName, query, "SHOWPLAN_XML", "estimated",
            ExtractFirstResultAsync, cancellationToken);

    public Task<string> GetActualPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken) =>
        GetPlanCoreAsync(serverName, databaseName, query, "STATISTICS XML", "actual",
            ExtractShowplanResultAsync, cancellationToken);

    private async Task<string> GetPlanCoreAsync(
        string serverName, string databaseName, string query,
        string settingName, string planTypeLabel,
        Func<SqlDataReader, CancellationToken, Task<string?>> extractPlanXml,
        CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        var validationError = QueryValidator.Validate(query);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        _logger.LogInformation("Getting {PlanType} plan on server {Server} database {Database} ({QueryLength} chars)", planTypeLabel, serverName, databaseName, query.Length);
        _logger.LogDebug("Query text: {Query}", query);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        try
        {
            await using var settingOn = new SqlCommand($"SET {settingName} ON", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            await settingOn.ExecuteNonQueryAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var planXml = await extractPlanXml(reader, cancellationToken);

            return planXml ?? string.Empty;
        }
        finally
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));
            await using var settingOff = new SqlCommand($"SET {settingName} OFF", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            try
            {
                await settingOff.ExecuteNonQueryAsync(cleanupCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset {SettingName} setting during cleanup", settingName);
            }
        }
    }

    private static async Task<string?> ExtractFirstResultAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (await reader.ReadAsync(cancellationToken))
            return reader.GetString(0);
        return null;
    }

    private static async Task<string?> ExtractShowplanResultAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        // Skip data result sets — the XML plan is in the result set with the showplan column
        string? planXml = null;
        do
        {
            if (reader.FieldCount == 1 && reader.GetName(0) == "Microsoft SQL Server 2005 XML Showplan")
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    planXml = reader.GetString(0);
                }
            }
        } while (await reader.NextResultAsync(cancellationToken));
        return planXml;
    }

    private static object FormatValue(object value) => value switch
    {
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };
}
