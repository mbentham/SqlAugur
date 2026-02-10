using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Services;

public sealed partial class DiagramService : IDiagramService
{
    private readonly SqlServerMcpOptions _options;
    private readonly ILogger<DiagramService> _logger;

    public DiagramService(
        IOptions<SqlServerMcpOptions> options,
        ILogger<DiagramService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    internal sealed record TableInfo(string Schema, string Name);

    internal sealed record ColumnInfo(
        string Schema, string TableName, string ColumnName, string DataType,
        int MaxLength, byte Precision, byte Scale, bool IsNullable,
        bool IsPrimaryKey, bool IsIdentity);

    internal sealed record ForeignKeyInfo(
        string FkName, string FkSchema, string FkTable, string FkColumn,
        string RefSchema, string RefTable, string RefColumn,
        bool IsNullable, bool IsUnique);

    public async Task<string> GenerateDiagramAsync(string serverName, string databaseName,
        string? schemaFilter, int maxTables, CancellationToken cancellationToken)
    {
        var serverConfig = _options.ResolveServer(serverName);

        _logger.LogInformation("Generating diagram on server {Server} database {Database} schema {Schema}", serverName, databaseName, schemaFilter ?? "all");

        await using var connection = new SqlConnection(serverConfig.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ChangeDatabaseAsync(databaseName, cancellationToken);

        var tables = await QueryTablesAsync(connection, schemaFilter, maxTables, cancellationToken);
        if (tables.Count == 0)
            return GenerateEmptyDiagram(serverName, databaseName, schemaFilter);

        await CreateTableFilterAsync(connection, tables, cancellationToken);
        var columns = await QueryColumnsAsync(connection, cancellationToken);
        var foreignKeys = await QueryForeignKeysAsync(connection, cancellationToken);

        return BuildPlantUml(serverName, databaseName, schemaFilter, maxTables, tables, columns, foreignKeys);
    }

    private async Task<List<TableInfo>> QueryTablesAsync(SqlConnection connection,
        string? schemaFilter, int maxTables, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
            """);

        if (schemaFilter is not null)
            sql.Append(" AND TABLE_SCHEMA = @schemaFilter");

        sql.Append(" ORDER BY TABLE_SCHEMA, TABLE_NAME");

        await using var cmd = new SqlCommand(sql.ToString(), connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        if (schemaFilter is not null)
            cmd.Parameters.AddWithValue("@schemaFilter", schemaFilter);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var tables = new List<TableInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (tables.Count >= maxTables)
                break;

            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    private async Task CreateTableFilterAsync(SqlConnection connection,
        List<TableInfo> tables, CancellationToken cancellationToken)
    {
        // Create temp table
        await using var createCmd = new SqlCommand(
            "CREATE TABLE #diagram_tables (SchemaName sysname NOT NULL, TableName sysname NOT NULL);",
            connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };
        await createCmd.ExecuteNonQueryAsync(cancellationToken);

        if (tables.Count == 0)
            return;

        // Use multiple batched INSERT statements to avoid SQL Server's 2100 parameter limit
        // Batch size of 500 means max 1000 parameters per batch (well under the 2100 limit)
        const int batchSize = 500;
        for (var batchStart = 0; batchStart < tables.Count; batchStart += batchSize)
        {
            var batchEnd = Math.Min(batchStart + batchSize, tables.Count);
            var batchCount = batchEnd - batchStart;

            var sb = new StringBuilder();
            sb.Append("INSERT INTO #diagram_tables (SchemaName, TableName) VALUES ");

            for (var i = 0; i < batchCount; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(CultureInfo.InvariantCulture, $"(@s{i}, @t{i})");
            }

            await using var insertCmd = new SqlCommand(sb.ToString(), connection)
            {
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            for (var i = 0; i < batchCount; i++)
            {
                var table = tables[batchStart + i];
                insertCmd.Parameters.AddWithValue($"@s{i}", table.Schema);
                insertCmd.Parameters.AddWithValue($"@t{i}", table.Name);
            }

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<List<ColumnInfo>> QueryColumnsAsync(SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.TABLE_SCHEMA,
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                COALESCE(c.CHARACTER_MAXIMUM_LENGTH, 0) AS MaxLength,
                CAST(COALESCE(c.NUMERIC_PRECISION, 0) AS tinyint) AS [Precision],
                CAST(COALESCE(c.NUMERIC_SCALE, 0) AS tinyint) AS Scale,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                CASE WHEN ixc.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                sc.is_identity AS IsIdentity
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN #diagram_tables dt ON dt.SchemaName = c.TABLE_SCHEMA AND dt.TableName = c.TABLE_NAME
            INNER JOIN sys.columns sc
                ON sc.object_id = OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME))
                AND sc.name = c.COLUMN_NAME
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.index_columns ic
                INNER JOIN sys.indexes ix ON ix.object_id = ic.object_id AND ix.index_id = ic.index_id
                WHERE ix.is_primary_key = 1
            ) ixc ON ixc.object_id = OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME))
                AND ixc.column_id = sc.column_id
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var columns = new List<ColumnInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnInfo(
                Schema: reader.GetString(0),
                TableName: reader.GetString(1),
                ColumnName: reader.GetString(2),
                DataType: reader.GetString(3),
                MaxLength: reader.GetInt32(4),
                Precision: reader.GetByte(5),
                Scale: reader.GetByte(6),
                IsNullable: reader.GetInt32(7) == 1,
                IsPrimaryKey: reader.GetInt32(8) == 1,
                IsIdentity: reader.GetBoolean(9)));
        }

        return columns;
    }

    private async Task<List<ForeignKeyInfo>> QueryForeignKeysAsync(SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fk.name AS FkName,
                SCHEMA_NAME(fk_tab.schema_id) AS FkSchema,
                fk_tab.name AS FkTable,
                fk_col.name AS FkColumn,
                SCHEMA_NAME(ref_tab.schema_id) AS RefSchema,
                ref_tab.name AS RefTable,
                ref_col.name AS RefColumn,
                sc.is_nullable AS IsNullable,
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.index_columns uic
                    INNER JOIN sys.indexes uix ON uix.object_id = uic.object_id AND uix.index_id = uic.index_id
                    WHERE uix.is_unique = 1
                      AND uic.object_id = fkc.parent_object_id
                      AND uic.column_id = fkc.parent_column_id
                      AND NOT EXISTS (
                          SELECT 1 FROM sys.index_columns uic2
                          WHERE uic2.object_id = uic.object_id AND uic2.index_id = uic.index_id
                            AND uic2.column_id <> uic.column_id
                      )
                ) THEN 1 ELSE 0 END AS IsUnique
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables fk_tab ON fk_tab.object_id = fkc.parent_object_id
            INNER JOIN sys.columns fk_col ON fk_col.object_id = fkc.parent_object_id AND fk_col.column_id = fkc.parent_column_id
            INNER JOIN sys.tables ref_tab ON ref_tab.object_id = fkc.referenced_object_id
            INNER JOIN sys.columns ref_col ON ref_col.object_id = fkc.referenced_object_id AND ref_col.column_id = fkc.referenced_column_id
            INNER JOIN sys.columns sc ON sc.object_id = fkc.parent_object_id AND sc.column_id = fkc.parent_column_id
            INNER JOIN #diagram_tables dt_fk
                ON dt_fk.SchemaName = SCHEMA_NAME(fk_tab.schema_id) AND dt_fk.TableName = fk_tab.name
            INNER JOIN #diagram_tables dt_ref
                ON dt_ref.SchemaName = SCHEMA_NAME(ref_tab.schema_id) AND dt_ref.TableName = ref_tab.name
            ORDER BY fk.name, fkc.constraint_column_id
            """;

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var foreignKeys = new List<ForeignKeyInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            foreignKeys.Add(new ForeignKeyInfo(
                FkName: reader.GetString(0),
                FkSchema: reader.GetString(1),
                FkTable: reader.GetString(2),
                FkColumn: reader.GetString(3),
                RefSchema: reader.GetString(4),
                RefTable: reader.GetString(5),
                RefColumn: reader.GetString(6),
                IsNullable: reader.GetBoolean(7),
                IsUnique: reader.GetInt32(8) == 1));
        }

        return foreignKeys;
    }

    internal static string BuildPlantUml(string serverName, string databaseName,
        string? schemaFilter, int maxTables, List<TableInfo> tables,
        List<ColumnInfo> columns, List<ForeignKeyInfo> foreignKeys)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine($"' ER Diagram: {SanitizePlantUmlText(databaseName)} on {SanitizePlantUmlText(serverName)}");
        sb.Append("' Schema: ");
        sb.Append(SanitizePlantUmlText(schemaFilter ?? "all"));
        sb.AppendLine($" | Tables: {tables.Count}");
        sb.AppendLine();
        sb.AppendLine("skinparam linetype ortho");
        sb.AppendLine();

        // Build a lookup of FK columns for marking
        var fkColumns = new HashSet<(string Schema, string Table, string Column)>();
        foreach (var fk in foreignKeys)
            fkColumns.Add((fk.FkSchema, fk.FkTable, fk.FkColumn));

        // Group columns by table
        var columnsByTable = columns
            .GroupBy(c => new TableInfo(c.Schema, c.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var table in tables)
        {
            var displayName = table.Schema == "dbo"
                ? SanitizePlantUmlText(table.Name)
                : $"{SanitizePlantUmlText(table.Schema)}.{SanitizePlantUmlText(table.Name)}";
            var alias = SanitizeAlias($"{table.Schema}_{table.Name}");

            sb.AppendLine($"entity \"{displayName}\" as {alias} {{");

            if (!columnsByTable.TryGetValue(table, out var tableCols))
            {
                sb.AppendLine("}");
                sb.AppendLine();
                continue;
            }

            var pkCols = tableCols.Where(c => c.IsPrimaryKey).ToList();
            var nonPkCols = tableCols.Where(c => !c.IsPrimaryKey).ToList();

            // PK columns above separator
            foreach (var col in pkCols)
            {
                var stereotypes = "<<PK>>";
                if (col.IsIdentity)
                    stereotypes += " <<IDENTITY>>";

                sb.AppendLine($"  * {SanitizePlantUmlText(col.ColumnName)} : {FormatDataType(col)} {stereotypes}");
            }

            if (pkCols.Count > 0 || nonPkCols.Count > 0)
                sb.AppendLine("  --");

            // Non-PK columns
            foreach (var col in nonPkCols)
            {
                var prefix = col.IsNullable ? "  " : "  *";
                var stereotype = fkColumns.Contains((col.Schema, col.TableName, col.ColumnName))
                    ? " <<FK>>"
                    : "";

                sb.Append(prefix);
                sb.Append(' ');
                sb.Append(SanitizePlantUmlText(col.ColumnName));
                sb.Append(" : ");
                sb.Append(FormatDataType(col));
                sb.AppendLine(stereotype);
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Relationships, deduplicated by FK name
        var emittedFks = new HashSet<string>();
        foreach (var fk in foreignKeys)
        {
            if (!emittedFks.Add(fk.FkName))
                continue; // Skip composite FK duplicates (already emitted)

            var refAlias = SanitizeAlias($"{fk.RefSchema}_{fk.RefTable}");
            var fkAlias = SanitizeAlias($"{fk.FkSchema}_{fk.FkTable}");

            // Determine cardinality
            string refSide = "||"; // referenced (parent) side is always mandatory one
            string fkSide;

            if (fk.IsUnique)
                fkSide = fk.IsNullable ? "o|" : "||"; // one-to-one
            else
                fkSide = fk.IsNullable ? "o{" : "|{"; // one-to-many

            sb.AppendLine($"{refAlias} {refSide}--{fkSide} {fkAlias} : \"{SanitizePlantUmlText(fk.FkName)}\"");
        }

        if (tables.Count >= maxTables)
        {
            sb.AppendLine();
            sb.AppendLine($"' WARNING: Output truncated at {maxTables} tables. Increase maxTables to see more.");
        }

        sb.AppendLine();
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    internal static string GenerateEmptyDiagram(string serverName, string databaseName, string? schemaFilter)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine($"' ER Diagram: {SanitizePlantUmlText(databaseName)} on {SanitizePlantUmlText(serverName)}");
        sb.AppendLine($"' Schema: {SanitizePlantUmlText(schemaFilter ?? "all")} | Tables: 0");
        sb.AppendLine();
        sb.AppendLine("note \"No tables found\" as N1");
        sb.AppendLine();
        sb.AppendLine("@enduml");
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

    internal static string SanitizeAlias(string input)
        => AliasRegex().Replace(input, "_");

    /// <summary>
    /// Strips characters that could break out of PlantUML comments or inject directives.
    /// </summary>
    internal static string SanitizePlantUmlText(string input)
        => PlantUmlUnsafeChars().Replace(input, "");

    [GeneratedRegex(@"[.\s\-]")]
    private static partial Regex AliasRegex();

    [GeneratedRegex(@"[\r\n@""{}]")]
    private static partial Regex PlantUmlUnsafeChars();
}
