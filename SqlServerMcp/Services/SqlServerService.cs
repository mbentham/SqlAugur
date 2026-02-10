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

        _logger.LogInformation("Executing query on server {Server} database {Database}: {Query}", serverName, databaseName, query);

        // Execute query
        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        await using var command = new SqlCommand(query, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Build column metadata
        var columns = new List<Dictionary<string, string>>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(new Dictionary<string, string>
            {
                ["name"] = reader.GetName(i),
                ["type"] = reader.GetFieldType(i)?.Name ?? "Unknown"
            });
        }

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
            ["server"] = serverName,
            ["rowCount"] = rows.Count,
            ["truncated"] = truncated,
            ["columns"] = columns,
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
            "SELECT name, database_id, state_desc, create_date FROM sys.databases ORDER BY name",
            connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var databases = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            databases.Add(new Dictionary<string, object?>
            {
                ["name"] = reader.GetString(0),
                ["databaseId"] = reader.GetInt32(1),
                ["state"] = reader.GetString(2),
                ["createDate"] = reader.GetDateTime(3).ToString("O")
            });
        }

        var response = new Dictionary<string, object?>
        {
            ["server"] = serverName,
            ["databaseCount"] = databases.Count,
            ["databases"] = databases
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    public async Task<string> GetEstimatedPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        var validationError = QueryValidator.Validate(query);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        _logger.LogInformation("Getting estimated plan on server {Server} database {Database}: {Query}", serverName, databaseName, query);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        try
        {
            await using var showplanOn = new SqlCommand("SET SHOWPLAN_XML ON", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            await showplanOn.ExecuteNonQueryAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            string? planXml = null;
            if (await reader.ReadAsync(cancellationToken))
            {
                planXml = reader.GetString(0);
            }

            var response = new Dictionary<string, object?>
            {
                ["server"] = serverName,
                ["database"] = databaseName,
                ["planType"] = "estimated",
                ["planXml"] = planXml
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        finally
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));
            await using var showplanOff = new SqlCommand("SET SHOWPLAN_XML OFF", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            try
            {
                await showplanOff.ExecuteNonQueryAsync(cleanupCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset SHOWPLAN_XML setting during cleanup");
            }
        }
    }

    public async Task<string> GetActualPlanAsync(string serverName, string databaseName, string query, CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        var validationError = QueryValidator.Validate(query);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        _logger.LogInformation("Getting actual plan on server {Server} database {Database}: {Query}", serverName, databaseName, query);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        try
        {
            await using var statsOn = new SqlCommand("SET STATISTICS XML ON", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            await statsOn.ExecuteNonQueryAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Skip data result sets — the XML plan is in the last result set
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

            var response = new Dictionary<string, object?>
            {
                ["server"] = serverName,
                ["database"] = databaseName,
                ["planType"] = "actual",
                ["planXml"] = planXml
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        finally
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));
            await using var statsOff = new SqlCommand("SET STATISTICS XML OFF", connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };
            try
            {
                await statsOff.ExecuteNonQueryAsync(cleanupCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset STATISTICS XML setting during cleanup");
            }
        }
    }

    private static object FormatValue(object value) => value switch
    {
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };
}
