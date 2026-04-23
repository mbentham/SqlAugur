using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlAugur.Services;

public static class QueryValidator
{
    /// <summary>
    /// Validates a SQL query for safety using ScriptDom AST parsing.
    /// Returns null if valid, or an error message string.
    /// </summary>
    public static string? Validate(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Query cannot be empty.";

        if (query.Length > 1_000_000)
            return "Query exceeds maximum allowed length (1,000,000 characters).";

        // Parse with the official T-SQL parser
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var parseErrors);

        if (parseErrors.Count > 0)
            return $"SQL parse error: {parseErrors[0].Message}";

        // The top-level fragment is a TSqlScript containing batches
        if (fragment is not TSqlScript script)
            return "Unable to parse query as T-SQL.";

        // Count total statements across all batches — must be exactly 1
        var totalStatements = 0;
        TSqlStatement? singleStatement = null;
        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                totalStatements++;
                singleStatement = statement;
                if (totalStatements > 1)
                    return "Multiple SQL statements are not allowed. Please provide a single SELECT query.";
            }
        }

        if (totalStatements == 0 || singleStatement is null)
            return "Query contains no SQL statements.";

        // Must be a SelectStatement — rejects INSERT, UPDATE, DELETE, DROP, EXEC, CREATE, etc.
        if (singleStatement is not SelectStatement selectStatement)
            return "Only SELECT queries are allowed.";

        // Block SELECT INTO (writes data to a new table)
        if (selectStatement.Into is not null)
            return "SELECT INTO is not allowed. Only read-only SELECT queries are permitted.";

        // Walk the AST to reject forbidden table references
        var visitor = new ForbiddenNodeVisitor();
        fragment.Accept(visitor);
        if (visitor.ErrorMessage is not null)
            return visitor.ErrorMessage;

        return null;
    }

    private sealed class ForbiddenNodeVisitor : TSqlFragmentVisitor
    {
        public string? ErrorMessage { get; private set; }

        public override void Visit(OpenRowsetTableReference node)
        {
            ErrorMessage ??= "OPENROWSET is not allowed.";
        }

        public override void Visit(BulkOpenRowset node)
        {
            ErrorMessage ??= "OPENROWSET BULK is not allowed.";
        }

        public override void Visit(OpenRowsetCosmos node)
        {
            ErrorMessage ??= "OPENROWSET with external data providers (e.g. Cosmos DB) is not allowed.";
        }

        public override void Visit(InternalOpenRowset node)
        {
            ErrorMessage ??= "Internal OPENROWSET variants are not allowed.";
        }

        public override void Visit(OpenQueryTableReference node)
        {
            ErrorMessage ??= "OPENQUERY is not allowed.";
        }

        public override void Visit(AdHocTableReference node)
        {
            ErrorMessage ??= "OPENDATASOURCE is not allowed.";
        }

        public override void Visit(OpenXmlTableReference node)
        {
            ErrorMessage ??= "OPENXML is not allowed.";
        }

        public override void Visit(NamedTableReference node)
        {
            if (node.SchemaObject.ServerIdentifier is not null)
                ErrorMessage ??= "Linked server references (four-part names) are not allowed.";
        }

        public override void Visit(LiteralOptimizerHint node)
        {
            if (node.HintKind == OptimizerHintKind.MaxRecursion)
                ErrorMessage ??= "MAXRECURSION hint is not allowed. The default recursion limit (100) applies.";
        }
    }
}
