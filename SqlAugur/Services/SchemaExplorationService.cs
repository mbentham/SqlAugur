using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;

namespace SqlAugur.Services;

public sealed class SchemaExplorationService : ISchemaExplorationService
{
    private readonly SqlAugurOptions _options;
    private readonly ILogger<SchemaExplorationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Dictionary<string, string> ObjectTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROCEDURE"] = "P",
        ["FUNCTION"] = "FN,IF,TF",
        ["VIEW"] = "V",
        ["TRIGGER"] = "TR",
    };

    public SchemaExplorationService(
        IOptions<SqlAugurOptions> options,
        ILogger<SchemaExplorationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ListProgrammableObjectsAsync(
        string serverName, string databaseName,
        IReadOnlyList<string>? includeSchemas, IReadOnlyList<string>? excludeSchemas,
        IReadOnlyList<string>? objectTypes,
        CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);
        var typeFilters = ResolveTypeFilters(objectTypes);

        _logger.LogInformation("Listing programmable objects on server {Server} database {Database}", serverName, databaseName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        var sql = new StringBuilder("""
            SELECT s.name AS SchemaName, o.name AS ObjectName,
                   CASE o.type
                       WHEN 'P'  THEN 'PROCEDURE'
                       WHEN 'FN' THEN 'FUNCTION'
                       WHEN 'IF' THEN 'FUNCTION'
                       WHEN 'TF' THEN 'FUNCTION'
                       WHEN 'V'  THEN 'VIEW'
                       WHEN 'TR' THEN 'TRIGGER'
                   END AS ObjectType,
                   o.create_date AS CreateDate,
                   o.modify_date AS ModifyDate
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.is_ms_shipped = 0
              AND o.type IN ('P','FN','IF','TF','V','TR')
            """);

        var parameters = new List<SqlParameter>();

        // Object type filter
        if (typeFilters is { Count: > 0 })
        {
            sql.Append(" AND o.type IN (");
            for (var i = 0; i < typeFilters.Count; i++)
            {
                if (i > 0) sql.Append(", ");
                sql.Append(CultureInfo.InvariantCulture, $"@type{i}");
                parameters.Add(new SqlParameter($"@type{i}", typeFilters[i]));
            }
            sql.Append(')');
        }

        // Schema filters
        AppendFilter(sql, parameters, "s.name", includeSchemas, "inclSchema", negate: false);
        if (includeSchemas is not { Count: > 0 })
            AppendFilter(sql, parameters, "s.name", excludeSchemas, "excl", negate: true);

        sql.Append(" ORDER BY s.name, o.name");

        await using var cmd = new SqlCommand(sql.ToString(), connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        foreach (var p in parameters)
            cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var objects = new List<object>();
        while (await reader.ReadAsync(cancellationToken))
        {
            objects.Add(new
            {
                schema = reader.GetString(0),
                name = reader.GetString(1),
                type = reader.GetString(2),
                createDate = reader.GetDateTime(3).ToString("O"),
                modifyDate = reader.GetDateTime(4).ToString("O"),
            });
        }

        return JsonSerializer.Serialize(objects, JsonOptions);
    }

    public async Task<string> GetObjectDefinitionAsync(
        string serverName, string databaseName,
        string objectName, string schemaName,
        CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Getting object definition for [{Schema}].[{Object}] on server {Server} database {Database}",
            schemaName, objectName, serverName, databaseName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        const string sql = """
            SELECT
                CASE o.type
                    WHEN 'P'  THEN 'PROCEDURE'
                    WHEN 'FN' THEN 'SCALAR FUNCTION'
                    WHEN 'IF' THEN 'INLINE TABLE-VALUED FUNCTION'
                    WHEN 'TF' THEN 'TABLE-VALUED FUNCTION'
                    WHEN 'V'  THEN 'VIEW'
                    WHEN 'TR' THEN 'TRIGGER'
                    ELSE o.type_desc
                END AS ObjectType,
                m.definition AS Definition
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
            WHERE s.name = @schemaName AND o.name = @objectName
              AND o.is_ms_shipped = 0
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schemaName", schemaName);
        cmd.Parameters.AddWithValue("@objectName", objectName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new ArgumentException($"Object '[{schemaName}].[{objectName}]' not found in database '{databaseName}'.");

        var objectType = reader.GetString(0);
        if (reader.IsDBNull(1))
            throw new ArgumentException($"Object '[{schemaName}].[{objectName}]' has no SQL definition (may be a CLR object or encrypted).");

        var definition = reader.GetString(1);

        var sb = new StringBuilder();
        sb.AppendLine($"## {objectType}: [{schemaName}].[{objectName}]");
        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine(definition);
        sb.AppendLine("```");

        return sb.ToString();
    }

    public async Task<string> GetExtendedPropertiesAsync(
        string serverName, string databaseName,
        string? schemaName, string? tableName, string? columnName,
        CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Getting extended properties on server {Server} database {Database}", serverName, databaseName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        var sql = new StringBuilder("""
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                ep.name AS PropertyName,
                CAST(ep.value AS NVARCHAR(4000)) AS PropertyValue
            FROM sys.extended_properties ep
            INNER JOIN sys.tables t ON t.object_id = ep.major_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            LEFT JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id
            WHERE ep.class = 1
            """);

        var parameters = new List<SqlParameter>();

        if (schemaName is not null)
        {
            sql.Append(" AND s.name = @schemaName");
            parameters.Add(new SqlParameter("@schemaName", schemaName));
        }

        if (tableName is not null)
        {
            sql.Append(" AND t.name = @tableName");
            parameters.Add(new SqlParameter("@tableName", tableName));
        }

        if (columnName is not null)
        {
            sql.Append(" AND c.name = @columnName");
            parameters.Add(new SqlParameter("@columnName", columnName));
        }

        sql.Append(" ORDER BY s.name, t.name, c.name, ep.name");

        await using var cmd = new SqlCommand(sql.ToString(), connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        foreach (var p in parameters)
            cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var properties = new List<object>();
        while (await reader.ReadAsync(cancellationToken))
        {
            properties.Add(new
            {
                schema = reader.GetString(0),
                table = reader.GetString(1),
                column = reader.IsDBNull(2) ? null : reader.GetString(2),
                propertyName = reader.GetString(3),
                propertyValue = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }

        return JsonSerializer.Serialize(properties, JsonOptions);
    }

    public async Task<string> GetObjectDependenciesAsync(
        string serverName, string databaseName,
        string objectName, string schemaName,
        CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Getting dependencies for [{Schema}].[{Object}] on server {Server} database {Database}",
            schemaName, objectName, serverName, databaseName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        // Both queries in a single batch to halve network round-trips
        const string sql = """
            -- References: what the object references
            SELECT
                COALESCE(rs.name, d.referenced_schema_name, 'dbo') AS ReferencedSchema,
                COALESCE(ro.name, d.referenced_entity_name) AS ReferencedName,
                CASE
                    WHEN ro.type IS NOT NULL THEN
                        CASE ro.type
                            WHEN 'P'  THEN 'PROCEDURE'
                            WHEN 'FN' THEN 'FUNCTION'
                            WHEN 'IF' THEN 'FUNCTION'
                            WHEN 'TF' THEN 'FUNCTION'
                            WHEN 'V'  THEN 'VIEW'
                            WHEN 'TR' THEN 'TRIGGER'
                            WHEN 'U'  THEN 'TABLE'
                            ELSE ro.type_desc
                        END
                    ELSE 'UNKNOWN'
                END AS ReferencedType
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON o.object_id = d.referencing_id
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.objects ro ON ro.object_id = d.referenced_id
            LEFT JOIN sys.schemas rs ON rs.schema_id = ro.schema_id
            WHERE s.name = @schemaName AND o.name = @objectName
            ORDER BY ReferencedSchema, ReferencedName;

            -- ReferencedBy: what references the object
            SELECT
                rs.name AS ReferencingSchema,
                ro.name AS ReferencingName,
                CASE ro.type
                    WHEN 'P'  THEN 'PROCEDURE'
                    WHEN 'FN' THEN 'FUNCTION'
                    WHEN 'IF' THEN 'FUNCTION'
                    WHEN 'TF' THEN 'FUNCTION'
                    WHEN 'V'  THEN 'VIEW'
                    WHEN 'TR' THEN 'TRIGGER'
                    WHEN 'U'  THEN 'TABLE'
                    ELSE ro.type_desc
                END AS ReferencingType
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON o.object_id = d.referenced_id
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            INNER JOIN sys.objects ro ON ro.object_id = d.referencing_id
            INNER JOIN sys.schemas rs ON rs.schema_id = ro.schema_id
            WHERE s.name = @schemaName AND o.name = @objectName
            ORDER BY ReferencingSchema, ReferencingName
            """;

        var references = new List<object>();
        var referencedBy = new List<object>();

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@schemaName", schemaName);
        cmd.Parameters.AddWithValue("@objectName", objectName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            references.Add(new
            {
                schema = reader.GetString(0),
                name = reader.GetString(1),
                type = reader.GetString(2),
            });
        }

        await reader.NextResultAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            referencedBy.Add(new
            {
                schema = reader.GetString(0),
                name = reader.GetString(1),
                type = reader.GetString(2),
            });
        }

        return JsonSerializer.Serialize(new { references, referencedBy }, JsonOptions);
    }

    private static List<string>? ResolveTypeFilters(IReadOnlyList<string>? objectTypes)
    {
        if (objectTypes is not { Count: > 0 })
            return null;

        var types = new HashSet<string>(StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var ot in objectTypes)
        {
            var trimmed = ot.Trim();
            if (ObjectTypeMap.TryGetValue(trimmed, out var mapped))
            {
                foreach (var t in mapped.Split(','))
                    types.Add(t);
            }
            else
            {
                invalid.Add(trimmed);
            }
        }

        if (invalid.Count > 0)
            throw new ArgumentException(
                $"Unrecognized object type(s): {string.Join(", ", invalid)}. Valid types: PROCEDURE, FUNCTION, VIEW, TRIGGER.");

        return types.Count > 0 ? types.ToList() : null;
    }

    private static void AppendFilter(StringBuilder sql, List<SqlParameter> parameters,
        string column, IReadOnlyList<string>? values, string paramPrefix, bool negate)
    {
        if (values is not { Count: > 0 })
            return;

        var keyword = negate ? "NOT IN" : "IN";
        for (var i = 0; i < values.Count; i++)
        {
            sql.Append(i == 0 ? $" AND {column} {keyword} (" : ", ");
            sql.Append(CultureInfo.InvariantCulture, $"@{paramPrefix}{i}");
            parameters.Add(new SqlParameter($"@{paramPrefix}{i}", values[i]));
        }
        sql.Append(')');
    }
}
