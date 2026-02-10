using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

public sealed class TableDescribeService : ITableDescribeService
{
    private readonly SqlServerMcpOptions _options;
    private readonly ILogger<TableDescribeService> _logger;

    public TableDescribeService(
        IOptions<SqlServerMcpOptions> options,
        ILogger<TableDescribeService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    internal sealed record ColumnInfo(
        int OrdinalPosition, string ColumnName, string DataType,
        int MaxLength, byte Precision, byte Scale, bool IsNullable,
        string? DefaultName, string? DefaultDefinition,
        bool IsIdentity, long IdentitySeed, long IdentityIncrement,
        bool IsComputed, string? ComputedDefinition, bool IsComputedPersisted);

    internal sealed record IndexInfo(
        string IndexName, string IndexType, bool IsUnique, bool IsPrimaryKey,
        string ColumnName, bool IsIncluded, int KeyOrdinal, string? FilterDefinition);

    internal sealed record ForeignKeyInfo(
        string FkName, string ColumnName, string RefSchema, string RefTable,
        string RefColumn, string DeleteAction, string UpdateAction);

    internal sealed record CheckConstraintInfo(string Name, string Definition);

    public async Task<string> DescribeTableAsync(string serverName, string databaseName,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Describing table [{Schema}].[{Table}] on server {Server} database {Database}", schemaName, tableName, serverName, databaseName);

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        // Validate table exists
        await ValidateTableExistsAsync(connection, schemaName, tableName, cancellationToken);

        var columns = await QueryColumnsAsync(connection, schemaName, tableName, cancellationToken);
        var indexes = await QueryIndexesAsync(connection, schemaName, tableName, cancellationToken);
        var foreignKeys = await QueryForeignKeysAsync(connection, schemaName, tableName, cancellationToken);
        var checkConstraints = await QueryCheckConstraintsAsync(connection, schemaName, tableName, cancellationToken);

        return BuildMarkdown(serverName, databaseName, schemaName, tableName,
            columns, indexes, foreignKeys, checkConstraints);
    }

    private async Task ValidateTableExistsAsync(SqlConnection connection,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table), 'U')";

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            throw new ArgumentException(
                $"Table '[{schemaName}].[{tableName}]' not found in database.");
        }
    }

    private async Task<List<ColumnInfo>> QueryColumnsAsync(SqlConnection connection,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.column_id AS OrdinalPosition,
                c.name AS ColumnName,
                t.name AS DataType,
                CASE
                    WHEN t.name IN ('nvarchar', 'nchar') AND c.max_length > 0 THEN c.max_length / 2
                    WHEN t.name IN ('nvarchar', 'nchar') AND c.max_length = -1 THEN -1
                    ELSE CAST(c.max_length AS int)
                END AS MaxLength,
                c.precision AS [Precision],
                c.scale AS Scale,
                c.is_nullable AS IsNullable,
                dc.name AS DefaultName,
                dc.definition AS DefaultDefinition,
                c.is_identity AS IsIdentity,
                ic.seed_value AS IdentitySeed,
                ic.increment_value AS IdentityIncrement,
                c.is_computed AS IsComputed,
                cc.definition AS ComputedDefinition,
                CAST(COALESCE(cc.is_persisted, 0) AS bit) AS IsComputedPersisted
            FROM sys.columns c
            INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
            WHERE c.object_id = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table))
            ORDER BY c.column_id
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var columns = new List<ColumnInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            long seed = 0, increment = 0;
            if (!reader.IsDBNull(10))
                seed = Convert.ToInt64(reader.GetValue(10));
            if (!reader.IsDBNull(11))
                increment = Convert.ToInt64(reader.GetValue(11));

            columns.Add(new ColumnInfo(
                OrdinalPosition: reader.GetInt32(0),
                ColumnName: reader.GetString(1),
                DataType: reader.GetString(2),
                MaxLength: reader.GetInt32(3),
                Precision: reader.GetByte(4),
                Scale: reader.GetByte(5),
                IsNullable: reader.GetBoolean(6),
                DefaultName: reader.IsDBNull(7) ? null : reader.GetString(7),
                DefaultDefinition: reader.IsDBNull(8) ? null : reader.GetString(8),
                IsIdentity: reader.GetBoolean(9),
                IdentitySeed: seed,
                IdentityIncrement: increment,
                IsComputed: reader.GetBoolean(12),
                ComputedDefinition: reader.IsDBNull(13) ? null : reader.GetString(13),
                IsComputedPersisted: reader.GetBoolean(14)));
        }

        return columns;
    }

    private async Task<List<IndexInfo>> QueryIndexesAsync(SqlConnection connection,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                i.name AS IndexName,
                i.type_desc AS IndexType,
                i.is_unique AS IsUnique,
                i.is_primary_key AS IsPrimaryKey,
                c.name AS ColumnName,
                ic.is_included_column AS IsIncluded,
                CAST(ic.key_ordinal AS int) AS KeyOrdinal,
                i.filter_definition AS FilterDefinition
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table))
              AND i.name IS NOT NULL
            ORDER BY i.index_id, ic.key_ordinal, ic.index_column_id
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var indexes = new List<IndexInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            indexes.Add(new IndexInfo(
                IndexName: reader.GetString(0),
                IndexType: reader.GetString(1),
                IsUnique: reader.GetBoolean(2),
                IsPrimaryKey: reader.GetBoolean(3),
                ColumnName: reader.GetString(4),
                IsIncluded: reader.GetBoolean(5),
                KeyOrdinal: reader.GetInt32(6),
                FilterDefinition: reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return indexes;
    }

    private async Task<List<ForeignKeyInfo>> QueryForeignKeysAsync(SqlConnection connection,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fk.name AS FkName,
                fk_col.name AS ColumnName,
                SCHEMA_NAME(ref_tab.schema_id) AS RefSchema,
                ref_tab.name AS RefTable,
                ref_col.name AS RefColumn,
                fk.delete_referential_action_desc AS DeleteAction,
                fk.update_referential_action_desc AS UpdateAction
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns fk_col ON fk_col.object_id = fkc.parent_object_id AND fk_col.column_id = fkc.parent_column_id
            INNER JOIN sys.tables ref_tab ON ref_tab.object_id = fkc.referenced_object_id
            INNER JOIN sys.columns ref_col ON ref_col.object_id = fkc.referenced_object_id AND ref_col.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table))
            ORDER BY fk.name, fkc.constraint_column_id
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var foreignKeys = new List<ForeignKeyInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            foreignKeys.Add(new ForeignKeyInfo(
                FkName: reader.GetString(0),
                ColumnName: reader.GetString(1),
                RefSchema: reader.GetString(2),
                RefTable: reader.GetString(3),
                RefColumn: reader.GetString(4),
                DeleteAction: reader.GetString(5),
                UpdateAction: reader.GetString(6)));
        }

        return foreignKeys;
    }

    private async Task<List<CheckConstraintInfo>> QueryCheckConstraintsAsync(SqlConnection connection,
        string schemaName, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                cc.name AS Name,
                cc.definition AS Definition
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table))
            ORDER BY cc.name
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var constraints = new List<CheckConstraintInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            constraints.Add(new CheckConstraintInfo(
                Name: reader.GetString(0),
                Definition: reader.GetString(1)));
        }

        return constraints;
    }

    internal static string BuildMarkdown(string serverName, string databaseName,
        string schemaName, string tableName, List<ColumnInfo> columns,
        List<IndexInfo> indexes, List<ForeignKeyInfo> foreignKeys,
        List<CheckConstraintInfo> checkConstraints)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# Table: [{schemaName}].[{tableName}]");
        sb.AppendLine($"**Server:** {serverName} | **Database:** {databaseName}");
        sb.AppendLine();

        // Columns
        sb.AppendLine("## Columns");
        sb.AppendLine("| # | Column | Type | Nullable | Default | Extra |");
        sb.AppendLine("|---|--------|------|----------|---------|-------|");

        foreach (var col in columns)
        {
            var type = FormatDataType(col);
            var nullable = col.IsNullable ? "YES" : "NO";
            var defaultVal = col.DefaultDefinition ?? "";

            var extras = new List<string>();
            if (col.IsIdentity)
                extras.Add($"IDENTITY({col.IdentitySeed},{col.IdentityIncrement})");
            if (col.IsComputed)
            {
                var persisted = col.IsComputedPersisted ? ", PERSISTED" : "";
                extras.Add($"COMPUTED: {col.ComputedDefinition}{persisted}");
            }

            var extra = string.Join("; ", extras);

            sb.AppendLine($"| {col.OrdinalPosition} | {col.ColumnName} | {type} | {nullable} | {defaultVal} | {extra} |");
        }

        sb.AppendLine();

        // Group indexes
        var indexGroups = indexes
            .GroupBy(i => i.IndexName)
            .Select(g => new
            {
                Name = g.Key,
                Type = g.First().IndexType,
                IsUnique = g.First().IsUnique,
                IsPrimaryKey = g.First().IsPrimaryKey,
                KeyColumns = string.Join(", ", g.Where(x => !x.IsIncluded).OrderBy(x => x.KeyOrdinal).Select(x => x.ColumnName)),
                IncludedColumns = string.Join(", ", g.Where(x => x.IsIncluded).Select(x => x.ColumnName)),
                FilterDefinition = g.First().FilterDefinition
            })
            .ToList();

        // Primary Key
        var pk = indexGroups.FirstOrDefault(i => i.IsPrimaryKey);
        if (pk is not null)
        {
            sb.AppendLine("## Primary Key");
            sb.AppendLine($"- **{pk.Name}**: {pk.KeyColumns}");
            sb.AppendLine();
        }

        // Indexes (non-PK)
        var nonPkIndexes = indexGroups.Where(i => !i.IsPrimaryKey).ToList();
        if (nonPkIndexes.Count > 0)
        {
            sb.AppendLine("## Indexes");
            sb.AppendLine("| Index | Type | Unique | Columns | Included | Filter |");
            sb.AppendLine("|-------|------|--------|---------|----------|--------|");

            foreach (var idx in nonPkIndexes)
            {
                var unique = idx.IsUnique ? "YES" : "NO";
                var included = idx.IncludedColumns.Length > 0 ? idx.IncludedColumns : "";
                var filter = idx.FilterDefinition ?? "";

                sb.AppendLine($"| {idx.Name} | {idx.Type} | {unique} | {idx.KeyColumns} | {included} | {filter} |");
            }

            sb.AppendLine();
        }

        // Foreign Keys
        if (foreignKeys.Count > 0)
        {
            // Group composite FKs
            var fkGroups = foreignKeys
                .GroupBy(fk => fk.FkName)
                .Select(g => new
                {
                    Name = g.Key,
                    Columns = string.Join(", ", g.Select(x => x.ColumnName)),
                    RefSchema = g.First().RefSchema,
                    RefTable = g.First().RefTable,
                    RefColumns = string.Join(", ", g.Select(x => x.RefColumn)),
                    DeleteAction = FormatAction(g.First().DeleteAction),
                    UpdateAction = FormatAction(g.First().UpdateAction)
                })
                .ToList();

            sb.AppendLine("## Foreign Keys");
            sb.AppendLine("| FK Name | Columns | References | On Delete | On Update |");
            sb.AppendLine("|---------|---------|------------|-----------|-----------|");

            foreach (var fk in fkGroups)
            {
                var references = $"[{fk.RefSchema}].[{fk.RefTable}] ({fk.RefColumns})";
                sb.AppendLine($"| {fk.Name} | {fk.Columns} | {references} | {fk.DeleteAction} | {fk.UpdateAction} |");
            }

            sb.AppendLine();
        }

        // Check Constraints
        if (checkConstraints.Count > 0)
        {
            sb.AppendLine("## Check Constraints");
            sb.AppendLine("| Constraint | Definition |");
            sb.AppendLine("|------------|------------|");

            foreach (var cc in checkConstraints)
            {
                sb.AppendLine($"| {cc.Name} | {cc.Definition} |");
            }

            sb.AppendLine();
        }

        // Default Constraints
        var defaults = columns.Where(c => c.DefaultName is not null).ToList();
        if (defaults.Count > 0)
        {
            sb.AppendLine("## Default Constraints");
            sb.AppendLine("| Constraint | Column | Definition |");
            sb.AppendLine("|------------|--------|------------|");

            foreach (var col in defaults)
            {
                sb.AppendLine($"| {col.DefaultName} | {col.ColumnName} | {col.DefaultDefinition} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    internal static string FormatDataType(ColumnInfo col)
    {
        return col.DataType.ToLowerInvariant() switch
        {
            "nvarchar" or "varchar" or "nchar" or "char" or "varbinary" or "binary"
                => col.MaxLength == -1
                    ? $"{col.DataType}(MAX)"
                    : $"{col.DataType}({col.MaxLength})",
            "decimal" or "numeric"
                => $"{col.DataType}({col.Precision},{col.Scale})",
            _ => col.DataType
        };
    }

    internal static string FormatAction(string action)
    {
        return action switch
        {
            "NO_ACTION" => "NO ACTION",
            "CASCADE" => "CASCADE",
            "SET_NULL" => "SET NULL",
            "SET_DEFAULT" => "SET DEFAULT",
            _ => action
        };
    }
}
