using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

public sealed class SqlServerService : ISqlServerService
{
    private readonly SqlAugurOptions _options;
    private readonly ILogger<SqlServerService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    // Inline results pass to a file past roughly this many characters: under Claude Code's
    // default MCP output cap (25,000 tokens), above which the client saves the response to its
    // own file as a single JSON line too long to page through.
    internal const int InlineCharLimit = 40_000;
    internal const int PreviewRows = 10;
    internal const int PreviewMaxStringLength = 100;

    public SqlServerService(
        IOptions<SqlAugurOptions> options,
        ILogger<SqlServerService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> GetServerNames()
    {
        return _options.Servers.Keys.OrderBy(k => k).ToList();
    }

    public async Task<string> ExecuteQueryAsync(string serverName, string databaseName, string query, bool toFile, CancellationToken cancellationToken)
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
        return await FormatResultAsync(reader, toFile, cancellationToken);
    }

    /// <summary>
    /// Returns the result inline as JSON <c>{truncated, rows}</c> while it stays under
    /// ResultFileThresholdRows and <see cref="InlineCharLimit"/>; past either (or when
    /// <paramref name="toFile"/> is set) streams it to a CSV file and returns the path,
    /// row count, columns and a short preview instead. MaxRows caps both paths.
    /// </summary>
    internal async Task<string> FormatResultAsync(DbDataReader reader, bool toFile, CancellationToken cancellationToken)
    {
        var columns = new string[reader.FieldCount];
        var types = new string[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
        {
            columns[i] = reader.GetName(i);
            types[i] = reader.GetDataTypeName(i);
        }

        var threshold = _options.ResultFileThresholdRows;
        var rows = new List<object?[]>();
        var truncated = false;
        var spill = toFile;
        long chars = 0;

        while (!spill && await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= _options.MaxRows)
            {
                truncated = true;
                break;
            }

            var values = ReadRow(reader);
            rows.Add(values);

            if (threshold > 0)
            {
                chars += EstimateChars(columns, values);
                spill = rows.Count > threshold || chars > InlineCharLimit;
            }
        }

        if (spill)
            return await WriteResultFileAsync(reader, columns, types, rows, cancellationToken);

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["truncated"] = truncated,
            ["rows"] = rows.Select(r => ToDictionary(columns, r)).ToList()
        }, JsonOptions);
    }

    private async Task<string> WriteResultFileAsync(
        DbDataReader reader, string[] columns, string[] types, List<object?[]> bufferedRows,
        CancellationToken cancellationToken)
    {
        var (path, writer) = ResultFiles.Create(_options.ResultsDirectory ?? ResultFiles.DefaultDirectory);
        var rowCount = 0;
        var truncated = false;
        var preview = new List<object?[]>();

        try
        {
            await using (writer)
            {
                ResultFiles.WriteRow(writer, columns);
                foreach (var row in bufferedRows)
                    Write(row);

                while (await reader.ReadAsync(cancellationToken))
                {
                    if (rowCount >= _options.MaxRows)
                    {
                        truncated = true;
                        break;
                    }
                    Write(ReadRow(reader));
                }
            }
        }
        catch
        {
            // Don't leave a partial result behind; the original exception is what matters.
            try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }

        _logger.LogInformation("Wrote {RowCount} rows to {Path}", rowCount, path);

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["truncated"] = truncated,
            ["rowCount"] = rowCount,
            ["file"] = path,
            ["columns"] = columns.Select((name, i) => new { name, type = types[i] }).ToList(),
            ["preview"] = preview.Select(r => ToDictionary(columns, r, PreviewMaxStringLength)).ToList(),
            ["note"] = "Full result is in the CSV file (UTF-8, header row, NULL written as an empty field). " +
                       "Process it with code rather than reading the whole file into context."
        }, JsonOptions);

        void Write(object?[] row)
        {
            ResultFiles.WriteRow(writer, row);
            rowCount++;
            if (preview.Count < PreviewRows)
                preview.Add(row);
        }
    }

    private static object?[] ReadRow(DbDataReader reader)
    {
        var values = new object?[reader.FieldCount];
        for (var i = 0; i < values.Length; i++)
            values[i] = reader.IsDBNull(i) ? null : FormatValue(reader.GetValue(i));
        return values;
    }

    private static Dictionary<string, object?> ToDictionary(string[] columns, object?[] values, int maxStringLength = int.MaxValue)
    {
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < columns.Length; i++)
            row[columns[i]] = values[i] is string s && s.Length > maxStringLength ? s[..maxStringLength] + "…" : values[i];
        return row;
    }

    // Rough serialized size of one row (key + value + punctuation). Only needs to be in the
    // right ballpark to keep inline responses under InlineCharLimit.
    private static long EstimateChars(string[] columns, object?[] values)
    {
        long total = 2;
        for (var i = 0; i < values.Length; i++)
            total += columns[i].Length + 6 + (values[i] is string s ? s.Length : 8);
        return total;
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
