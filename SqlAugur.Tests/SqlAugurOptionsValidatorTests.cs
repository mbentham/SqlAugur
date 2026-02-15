using SqlAugur.Configuration;

namespace SqlAugur.Tests;

public class SqlAugurOptionsValidatorTests
{
    private static SqlAugurOptions MakeValidOptions(string? azureKeyVaultUri = null) => new()
    {
        Servers = new Dictionary<string, SqlServerConnection>
        {
            ["prod"] = new() { ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;" }
        },
        MaxRows = 1000,
        CommandTimeoutSeconds = 30,
        AzureKeyVaultUri = azureKeyVaultUri
    };

    private readonly SqlAugurOptionsValidator _validator = new();

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
        var options = new SqlAugurOptions
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
        var options = new SqlAugurOptions
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
    // MaxConcurrentQueries
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [InlineData(-1, false)]
    public void MaxConcurrentQueries_Boundaries(int value, bool shouldSucceed)
    {
        var options = new SqlAugurOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["test"] = new() { ConnectionString = "Server=localhost;" }
            },
            MaxRows = 1000,
            CommandTimeoutSeconds = 30,
            MaxConcurrentQueries = value,
            MaxQueriesPerMinute = 60
        };

        var result = _validator.Validate(null, options);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    // ───────────────────────────────────────────────
    // MaxQueriesPerMinute
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    [InlineData(-1, false)]
    public void MaxQueriesPerMinute_Boundaries(int value, bool shouldSucceed)
    {
        var options = new SqlAugurOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["test"] = new() { ConnectionString = "Server=localhost;" }
            },
            MaxRows = 1000,
            CommandTimeoutSeconds = 30,
            MaxConcurrentQueries = 5,
            MaxQueriesPerMinute = value
        };

        var result = _validator.Validate(null, options);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    // ───────────────────────────────────────────────
    // AzureKeyVaultUri
    // ───────────────────────────────────────────────

    [Fact]
    public void AzureKeyVaultUri_Null_Passes()
    {
        var options = MakeValidOptions();
        // AzureKeyVaultUri defaults to null

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AzureKeyVaultUri_Empty_Passes()
    {
        var options = MakeValidOptions(azureKeyVaultUri: "");

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AzureKeyVaultUri_ValidHttps_Passes()
    {
        var options = MakeValidOptions(azureKeyVaultUri: "https://myvault.vault.azure.net/");

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AzureKeyVaultUri_HttpScheme_Fails()
    {
        var options = MakeValidOptions(azureKeyVaultUri: "http://myvault.vault.azure.net/");

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("https", result.FailureMessage);
    }

    [Fact]
    public void AzureKeyVaultUri_Malformed_Fails()
    {
        var options = MakeValidOptions(azureKeyVaultUri: "not a uri");

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("AzureKeyVaultUri", result.FailureMessage);
    }

    [Fact]
    public void AzureKeyVaultUri_Relative_Fails()
    {
        var options = MakeValidOptions(azureKeyVaultUri: "/relative/path");

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("AzureKeyVaultUri", result.FailureMessage);
    }

    // ───────────────────────────────────────────────
    // TryValidateKeyVaultUri (shared helper)
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryValidateKeyVaultUri_NullOrEmpty_ReturnsTrue(string? input)
    {
        var result = SqlAugurOptionsValidator.TryValidateKeyVaultUri(input, out var uri, out var error);

        Assert.True(result);
        Assert.Null(uri);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateKeyVaultUri_ValidHttps_ReturnsTrueWithUri()
    {
        var result = SqlAugurOptionsValidator.TryValidateKeyVaultUri(
            "https://myvault.vault.azure.net/", out var uri, out var error);

        Assert.True(result);
        Assert.NotNull(uri);
        Assert.Equal("https", uri!.Scheme);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateKeyVaultUri_HttpScheme_ReturnsFalse()
    {
        var result = SqlAugurOptionsValidator.TryValidateKeyVaultUri(
            "http://myvault.vault.azure.net/", out var uri, out var error);

        Assert.False(result);
        Assert.Null(uri);
        Assert.NotNull(error);
        Assert.Contains("https", error!);
    }

    [Fact]
    public void TryValidateKeyVaultUri_Malformed_ReturnsFalse()
    {
        var result = SqlAugurOptionsValidator.TryValidateKeyVaultUri(
            "not a uri", out var uri, out var error);

        Assert.False(result);
        Assert.Null(uri);
        Assert.NotNull(error);
        Assert.Contains("AzureKeyVaultUri", error!);
    }

    // ───────────────────────────────────────────────
    // Multiple errors accumulated
    // ───────────────────────────────────────────────

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var options = new SqlAugurOptions
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
