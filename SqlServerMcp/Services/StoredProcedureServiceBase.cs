using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

public abstract class StoredProcedureServiceBase
{
    protected readonly SqlServerMcpOptions Options;
    private readonly ILogger _logger;
    private readonly HashSet<string> _allowedProcedures;
    private readonly string[] _blockedParameters;
    private readonly string _blockedParameterReason;
    private readonly string _procedureNotFoundMessage;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    protected StoredProcedureServiceBase(
        IOptions<SqlServerMcpOptions> options,
        ILogger logger,
        HashSet<string> allowedProcedures,
        string[] blockedParameters,
        string blockedParameterReason,
        string procedureNotFoundMessage)
    {
        Options = options.Value;
        _logger = logger;
        _allowedProcedures = allowedProcedures;
        _blockedParameters = blockedParameters;
        _blockedParameterReason = blockedParameterReason;
        _procedureNotFoundMessage = procedureNotFoundMessage;
    }

    protected async Task<string> ExecuteProcedureAsync(
        string serverName,
        string procedureName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (!_allowedProcedures.Contains(procedureName))
            throw new InvalidOperationException(
                $"Procedure '{procedureName}' is not in the allowed list.");

        foreach (var paramName in parameters.Keys)
        {
            if (_blockedParameters.Any(blocked =>
                paramName.Equals(blocked, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Parameter '{paramName}' is not allowed ({_blockedParameterReason}).");
            }
        }

        var serverConfig = Options.ResolveServer(serverName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = Options.CommandTimeoutSeconds
        };

        foreach (var (name, value) in parameters)
        {
            if (value is not null)
                command.Parameters.AddWithValue(name, value);
        }

        _logger.LogInformation("Executing {Procedure} on server {Server}", procedureName, serverName);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await FormatResultSetsAsync(reader, serverName, procedureName, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            throw new InvalidOperationException(
                $"Stored procedure '{procedureName}' not found on server '{serverName}'. " +
                _procedureNotFoundMessage);
        }
    }

    private async Task<string> FormatResultSetsAsync(
        SqlDataReader reader,
        string serverName,
        string procedureName,
        CancellationToken cancellationToken)
    {
        var resultSets = new List<Dictionary<string, object?>>();

        do
        {
            if (reader.FieldCount == 0)
                continue;

            var columns = new List<Dictionary<string, string>>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new Dictionary<string, string>
                {
                    ["name"] = reader.GetName(i),
                    ["type"] = reader.GetFieldType(i)?.Name ?? "Unknown"
                });
            }

            var rows = new List<Dictionary<string, object?>>();
            var truncated = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= Options.MaxRows)
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

            resultSets.Add(new Dictionary<string, object?>
            {
                ["columns"] = columns,
                ["rows"] = rows,
                ["rowCount"] = rows.Count,
                ["truncated"] = truncated
            });

        } while (await reader.NextResultAsync(cancellationToken));

        var response = new Dictionary<string, object?>
        {
            ["server"] = serverName,
            ["procedureName"] = procedureName,
            ["resultSets"] = resultSets
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    protected static void AddIfNotNull(Dictionary<string, object?> parameters, string name, object? value)
    {
        if (value is not null)
            parameters[name] = value;
    }

    protected static void AddBoolParam(Dictionary<string, object?> parameters, string name, bool? value)
    {
        if (value.HasValue)
            parameters[name] = value.Value ? 1 : 0;
    }

    internal static object FormatValue(object value) => value switch
    {
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };
}
