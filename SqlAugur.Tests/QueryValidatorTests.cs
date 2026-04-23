using SqlAugur.Services;

namespace SqlAugur.Tests;

public class QueryValidatorTests
{
    // ───────────────────────────────────────────────
    // Valid queries — should return null (no error)
    // ───────────────────────────────────────────────

    [Fact]
    public void SimpleSelect_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT 1"));
    }

    [Fact]
    public void SelectFrom_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT * FROM users"));
    }

    [Fact]
    public void SelectWithWhereClause_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT id, name FROM users WHERE id = 42"));
    }

    [Fact]
    public void WithCte_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "WITH cte AS (SELECT id FROM users) SELECT * FROM cte"));
    }

    [Fact]
    public void NestedCte_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "WITH a AS (SELECT 1 AS x), b AS (SELECT x FROM a) SELECT * FROM b"));
    }

    [Fact]
    public void Subquery_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM (SELECT id FROM users) AS sub"));
    }

    [Fact]
    public void SelectWithSingleLineComment_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT 1 -- this is a comment"));
    }

    [Fact]
    public void SelectWithBlockComment_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT /* a comment */ 1"));
    }

    [Fact]
    public void SelectWithStringLiteral_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users WHERE name = 'Alice'"));
    }

    [Fact]
    public void SelectWithEscapedQuoteInString_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users WHERE name = 'O''Brien'"));
    }

    [Fact]
    public void MixedCaseSelect_IsValid()
    {
        Assert.Null(QueryValidator.Validate("sElEcT 1"));
    }

    [Fact]
    public void LeadingWhitespace_IsValid()
    {
        Assert.Null(QueryValidator.Validate("   \t\n  SELECT 1"));
    }

    [Fact]
    public void TrailingSemicolon_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT 1;"));
    }

    [Fact]
    public void KeywordInsideStringLiteral_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users WHERE status = 'DELETE'"));
    }

    [Fact]
    public void KeywordInsideBlockComment_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT /* DROP TABLE users */ 1"));
    }

    [Fact]
    public void KeywordInsideLineComment_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT 1 -- DROP TABLE users"));
    }

    [Fact]
    public void IntoInsideStringLiteral_NotFalsePositive()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users WHERE notes = 'inserted into table'"));
    }

    // ───────────────────────────────────────────────
    // Blocked keywords — should return error message
    // ───────────────────────────────────────────────

    [Fact]
    public void Insert_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("INSERT INTO users VALUES (1)"));
    }

    [Fact]
    public void Update_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("UPDATE users SET name = 'x'"));
    }

    [Fact]
    public void Delete_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("DELETE FROM users"));
    }

    [Fact]
    public void Drop_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("DROP TABLE users"));
    }

    [Fact]
    public void Alter_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("ALTER TABLE users ADD col INT"));
    }

    [Fact]
    public void Create_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("CREATE TABLE t (id INT)"));
    }

    [Fact]
    public void Truncate_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("TRUNCATE TABLE users"));
    }

    [Fact]
    public void Exec_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("EXEC sp_help"));
    }

    [Fact]
    public void Execute_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("EXECUTE sp_help"));
    }

    [Fact]
    public void XpCmdshell_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT 1; EXEC xp_cmdshell 'whoami'"));
    }

    [Fact]
    public void Waitfor_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT 1; WAITFOR DELAY '00:00:10'"));
    }

    [Fact]
    public void Merge_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "MERGE INTO t USING s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.x = s.x;"));
    }

    [Fact]
    public void Openrowset_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT * FROM OPENROWSET('SQLNCLI', 'server', 'query')"));
    }

    [Fact]
    public void OpenrowsetBulk_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT BulkColumn FROM OPENROWSET(BULK 'C:\\file.txt', SINGLE_CLOB) AS t"));
    }

    [Fact]
    public void Dbcc_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("DBCC CHECKDB"));
    }

    [Fact]
    public void Shutdown_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("SHUTDOWN"));
    }

    [Fact]
    public void Backup_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("BACKUP DATABASE master TO DISK = 'x'"));
    }

    [Fact]
    public void Restore_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("RESTORE DATABASE master FROM DISK = 'x'"));
    }

    // ───────────────────────────────────────────────
    // CRITICAL-1: Comment/string stripping bypass
    // ───────────────────────────────────────────────

    [Fact]
    public void Critical1_CommentInsideString_DropTable()
    {
        // PoC from security review: -- inside a string literal
        // should not be treated as a comment
        var query = "SELECT * FROM users WHERE name = '\n-- ' ; DROP TABLE audit_log; SELECT '\n'";
        var result = QueryValidator.Validate(query);
        Assert.NotNull(result);
    }

    [Fact]
    public void Critical1_CommentInsideString_ExecVariant()
    {
        var query = "SELECT * FROM t WHERE x = '\n-- ' ; EXEC xp_cmdshell 'whoami'; SELECT '\n'";
        var result = QueryValidator.Validate(query);
        Assert.NotNull(result);
    }

    [Fact]
    public void Critical1_BlockCommentInsideString()
    {
        // /* inside a string is not a real comment opener
        var query = "SELECT * FROM t WHERE x = '/* ' ; DROP TABLE t; SELECT ' */'";
        var result = QueryValidator.Validate(query);
        Assert.NotNull(result);
    }

    // ───────────────────────────────────────────────
    // CRITICAL-2: SELECT INTO not blocked
    // ───────────────────────────────────────────────

    [Fact]
    public void Critical2_SelectInto_TempTable()
    {
        var result = QueryValidator.Validate(
            "SELECT * INTO #stolen_data FROM credit_cards");
        Assert.NotNull(result);
    }

    [Fact]
    public void Critical2_SelectInto_PermanentTable()
    {
        var result = QueryValidator.Validate(
            "SELECT * INTO exfil_table FROM passwords");
        Assert.NotNull(result);
    }

    [Fact]
    public void Critical2_SelectInto_MixedCase()
    {
        var result = QueryValidator.Validate(
            "SELECT * InTo #t FROM users");
        Assert.NotNull(result);
    }

    // ───────────────────────────────────────────────
    // MEDIUM-1: Nested block comments
    // ───────────────────────────────────────────────

    [Fact]
    public void NestedBlockComment_TwoDeep()
    {
        // /* outer /* inner */ still in outer */ should all be stripped
        Assert.Null(QueryValidator.Validate(
            "SELECT /* outer /* inner */ still in outer */ 1"));
    }

    [Fact]
    public void NestedBlockComment_ThreeDeep()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT /* a /* b /* c */ b */ a */ 1"));
    }

    [Fact]
    public void NestedBlockComment_WithKeywordHidden()
    {
        // DROP inside nested comment should be stripped
        Assert.Null(QueryValidator.Validate(
            "SELECT /* /* DROP TABLE users */ */ 1"));
    }

    // ───────────────────────────────────────────────
    // Edge cases
    // ───────────────────────────────────────────────

    [Fact]
    public void EmptyString_ReturnsError()
    {
        Assert.NotNull(QueryValidator.Validate(""));
    }

    [Fact]
    public void NullInput_ReturnsError()
    {
        Assert.NotNull(QueryValidator.Validate(null!));
    }

    [Fact]
    public void WhitespaceOnly_ReturnsError()
    {
        Assert.NotNull(QueryValidator.Validate("   \t\n  "));
    }

    [Fact]
    public void MultipleStatements_Semicolon()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT 1; SELECT 2"));
    }

    [Fact]
    public void SelectWithJoin_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT u.id, o.total FROM users u INNER JOIN orders o ON u.id = o.user_id"));
    }

    [Fact]
    public void SelectWithGroupByHaving_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT status, COUNT(*) FROM users GROUP BY status HAVING COUNT(*) > 1"));
    }

    [Fact]
    public void SelectWithUnion_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT id FROM users UNION ALL SELECT id FROM admins"));
    }

    // ───────────────────────────────────────────────
    // HIGH-1: Semicolon-less multi-statement batches
    // ───────────────────────────────────────────────

    [Fact]
    public void SemicolonlessDoubleSelect_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("SELECT 1 SELECT 2"));
    }

    [Fact]
    public void SemicolonlessTripleSelect_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("SELECT 1 SELECT 2 SELECT 3"));
    }

    [Fact]
    public void UnionAllSelect_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT 1 UNION ALL SELECT 2"));
    }

    [Fact]
    public void IntersectSelect_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT 1 INTERSECT SELECT 2"));
    }

    [Fact]
    public void ExceptSelect_IsValid()
    {
        Assert.Null(QueryValidator.Validate("SELECT 1 EXCEPT SELECT 2"));
    }

    [Fact]
    public void CteWithUnion_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "WITH cte AS (SELECT 1 AS x) SELECT x FROM cte UNION ALL SELECT 2"));
    }

    [Fact]
    public void SubqueryInWhere_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM t WHERE id IN (SELECT id FROM s)"));
    }

    // ───────────────────────────────────────────────
    // HIGH-2: Linked server four-part names
    // ───────────────────────────────────────────────

    [Fact]
    public void FourPartName_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT * FROM [LinkedServer].[Database].[dbo].[Table1]"));
    }

    [Fact]
    public void FourPartName_Unquoted_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT * FROM LinkedServer.Database.dbo.Table1"));
    }

    [Fact]
    public void FourPartName_InJoin_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT * FROM local_table t INNER JOIN [RemoteServer].[db].[dbo].[t2] r ON t.id = r.id"));
    }

    [Fact]
    public void ThreePartName_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM OtherDb.dbo.Users"));
    }

    [Fact]
    public void TwoPartName_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM dbo.Users"));
    }

    [Fact]
    public void OnePartName_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM Users"));
    }

    // ───────────────────────────────────────────────
    // Other edge cases
    // ───────────────────────────────────────────────

    [Fact]
    public void GrantInSelect_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT 1; GRANT ALL TO public"));
    }

    [Fact]
    public void RevokeInSelect_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT 1; REVOKE ALL FROM public"));
    }

    // ───────────────────────────────────────────────
    // MAXRECURSION hint blocking
    // ───────────────────────────────────────────────

    [Fact]
    public void MaxRecursion_Zero_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "WITH cte AS (SELECT 1 AS x UNION ALL SELECT x+1 FROM cte) SELECT * FROM cte OPTION (MAXRECURSION 0)"));
    }

    [Fact]
    public void MaxRecursion_LargeValue_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "WITH cte AS (SELECT 1 AS x UNION ALL SELECT x+1 FROM cte) SELECT TOP 10 * FROM cte OPTION (MAXRECURSION 32767)"));
    }

    [Fact]
    public void MaxRecursion_SmallValue_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "WITH cte AS (SELECT 1 AS x UNION ALL SELECT x+1 FROM cte) SELECT TOP 10 * FROM cte OPTION (MAXRECURSION 50)"));
    }

    [Fact]
    public void OptionRecompile_IsAllowed()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users OPTION (RECOMPILE)"));
    }

    [Fact]
    public void OptionHashJoin_IsAllowed()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id OPTION (HASH JOIN)"));
    }

    [Fact]
    public void OpenXml_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate(
            "SELECT * FROM OPENXML(@hdoc, '/root/row')"));
    }

    // ───────────────────────────────────────────────
    // Additional blocked constructs
    // ───────────────────────────────────────────────

    [Fact]
    public void OpenQuery_IsBlocked()
    {
        var result = QueryValidator.Validate(
            "SELECT * FROM OPENQUERY(LinkedSrv, 'SELECT 1')");
        Assert.NotNull(result);
        Assert.Contains("OPENQUERY", result);
    }

    [Fact]
    public void OpenDataSource_IsBlocked()
    {
        var result = QueryValidator.Validate(
            "SELECT * FROM OPENDATASOURCE('SQLNCLI', 'Data Source=srv;Integrated Security=SSPI')...Table1");
        Assert.NotNull(result);
        Assert.Contains("OPENDATASOURCE", result);
    }

    [Fact]
    public void Deny_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("DENY SELECT ON dbo.Users TO public"));
    }

    [Fact]
    public void Set_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("SET NOCOUNT ON"));
    }

    [Fact]
    public void Declare_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("DECLARE @x INT = 1"));
    }

    [Fact]
    public void BulkInsert_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("BULK INSERT dbo.t FROM 'file.csv'"));
    }

    [Fact]
    public void Print_IsBlocked()
    {
        Assert.NotNull(QueryValidator.Validate("PRINT 'hello'"));
    }

    [Fact]
    public void GoBatchSeparator_IsBlocked()
    {
        // GO creates multiple batches — validator requires exactly 1 statement
        Assert.NotNull(QueryValidator.Validate("SELECT 1\nGO\nSELECT 2"));
    }

    // ───────────────────────────────────────────────
    // Additional valid edge cases
    // ───────────────────────────────────────────────

    [Fact]
    public void VeryLongQuery_IsValid()
    {
        var longCondition = string.Join(" OR ", Enumerable.Range(1, 200).Select(i => $"id = {i}"));
        Assert.Null(QueryValidator.Validate($"SELECT * FROM users WHERE {longCondition}"));
    }

    [Fact]
    public void UnicodeIdentifier_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM [Empleados_étranger]"));
    }

    [Fact]
    public void SelectInto_InSubquery_NotFalsePositive()
    {
        // INTO only appears in a string literal, not as SELECT INTO
        Assert.Null(QueryValidator.Validate(
            "SELECT * FROM (SELECT id, name FROM users) AS sub WHERE name LIKE '%test%'"));
    }

    [Fact]
    public void RecursiveCte_WithDefaultRecursion_IsValid()
    {
        Assert.Null(QueryValidator.Validate(
            "WITH cte AS (SELECT 1 AS x UNION ALL SELECT x + 1 FROM cte WHERE x < 10) SELECT * FROM cte"));
    }

    // ───────────────────────────────────────────────
    // Query length limit
    // ───────────────────────────────────────────────

    [Fact]
    public void Validate_QueryExceedsMaxLength_ReturnsError()
    {
        var query = "SELECT " + new string('x', 1_000_001);
        var result = QueryValidator.Validate(query);
        Assert.NotNull(result);
        Assert.Contains("maximum allowed length", result);
    }
}
