using SqlServerMcp.Configuration;

namespace SqlServerMcp.Tests;

public class SqlServerMcpOptionsValidatorTests
{
    private static SqlServerMcpOptions MakeValidOptions() => new()
    {
        Servers = new Dictionary<string, SqlServerConnection>
        {
            ["prod"] = new() { ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;" }
        },
        MaxRows = 1000,
        CommandTimeoutSeconds = 30
    };

    private readonly SqlServerMcpOptionsValidator _validator = new();

    // ───────────────────────────────────────────────
    // Servers
    // ───────────────────────────────────────────────

    [Fact]
    public void ZeroServers_Fails()
    {
        var options = MakeValidOptions();
        options.Servers.Clear();

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("At least one server", result.FailureMessage);
    }

    [Fact]
    public void EmptyConnectionString_Fails()
    {
        var options = MakeValidOptions();
        options.Servers["bad"] = new SqlServerConnection { ConnectionString = "" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("empty ConnectionString", result.FailureMessage);
    }

    [Fact]
    public void WhitespaceConnectionString_Fails()
    {
        var options = MakeValidOptions();
        options.Servers["bad"] = new SqlServerConnection { ConnectionString = "   " };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("empty ConnectionString", result.FailureMessage);
    }

    [Fact]
    public void InvalidConnectionString_Fails()
    {
        var options = MakeValidOptions();
        options.Servers["bad"] = new SqlServerConnection { ConnectionString = "not a valid=connection;string=;;==" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("invalid ConnectionString", result.FailureMessage);
    }

    [Fact]
    public void ValidConnectionString_Passes()
    {
        var options = MakeValidOptions();

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MultipleServers_AllValid_Passes()
    {
        var options = MakeValidOptions();
        options.Servers["dev"] = new SqlServerConnection
        {
            ConnectionString = "Server=devhost;Database=master;Trusted_Connection=True;"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MultipleServers_OneInvalid_Fails()
    {
        var options = MakeValidOptions();
        options.Servers["broken"] = new SqlServerConnection { ConnectionString = "" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("broken", result.FailureMessage);
    }

    // ───────────────────────────────────────────────
    // MaxRows
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100_000, true)]
    [InlineData(100_001, false)]
    [InlineData(-1, false)]
    public void MaxRows_Boundaries(int value, bool shouldSucceed)
    {
        var options = new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["test"] = new() { ConnectionString = "Server=localhost;" }
            },
            MaxRows = value,
            CommandTimeoutSeconds = 30
        };

        var result = _validator.Validate(null, options);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    // ───────────────────────────────────────────────
    // Timeouts
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void CommandTimeoutSeconds_Boundaries(int value, bool shouldSucceed)
    {
        var options = new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["test"] = new() { ConnectionString = "Server=localhost;" }
            },
            MaxRows = 1000,
            CommandTimeoutSeconds = value
        };

        var result = _validator.Validate(null, options);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    // ───────────────────────────────────────────────
    // Multiple errors accumulated
    // ───────────────────────────────────────────────

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var options = new SqlServerMcpOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>(),
            MaxRows = 0,
            CommandTimeoutSeconds = 0
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        // Should have at least: no servers + MaxRows + timeout = 3 errors
        Assert.Contains("At least one server", result.FailureMessage);
        Assert.Contains("MaxRows", result.FailureMessage);
        Assert.Contains("CommandTimeoutSeconds", result.FailureMessage);
    }
}
