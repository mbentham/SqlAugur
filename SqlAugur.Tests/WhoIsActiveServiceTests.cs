using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;
using SqlAugur.Services;

namespace SqlAugur.Tests;

public class WhoIsActiveServiceTests
{
    private readonly WhoIsActiveService _service;

    public WhoIsActiveServiceTests()
    {
        var options = Options.Create(new SqlAugurOptions
        {
            Servers = new Dictionary<string, SqlServerConnection>
            {
                ["testserver"] = new() { ConnectionString = "Server=localhost;Database=master;Encrypt=True;TrustServerCertificate=False;" }
            },
            MaxRows = 100,
            CommandTimeoutSeconds = 120
        });

        _service = new WhoIsActiveService(options, NullLogger<WhoIsActiveService>.Instance);
    }

    // ───────────────────────────────────────────────
    // Allowed procedure whitelist
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("sp_WhoIsActive")]
    public void AllowedProcedure_IsInWhitelist(string procedureName)
    {
        Assert.Contains(procedureName, WhoIsActiveService.AllowedProcedures);
    }

    [Theory]
    [InlineData("SP_WHOISACTIVE")]
    [InlineData("sp_whoisactive")]
    [InlineData("Sp_WhoIsActive")]
    public void AllowedProcedure_CaseInsensitive(string procedureName)
    {
        Assert.Contains(procedureName, WhoIsActiveService.AllowedProcedures);
    }

    [Theory]
    [InlineData("sp_who")]
    [InlineData("sp_who2")]
    [InlineData("xp_cmdshell")]
    [InlineData("sp_MSforeachdb")]
    [InlineData("sp_executesql")]
    [InlineData("sp_Blitz")]
    [InlineData("sp_PressureDetector")]
    public void DisallowedProcedure_IsNotInWhitelist(string procedureName)
    {
        Assert.DoesNotContain(procedureName, WhoIsActiveService.AllowedProcedures);
    }

    // ───────────────────────────────────────────────
    // Blocked parameters
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("@destination_table")]
    [InlineData("@return_schema")]
    [InlineData("@schema")]
    [InlineData("@help")]
    public void BlockedParameter_IsInBlockList(string parameterName)
    {
        Assert.Contains(WhoIsActiveService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("@filter")]
    [InlineData("@filter_type")]
    [InlineData("@sort_order")]
    [InlineData("@get_plans")]
    [InlineData("@delta_interval")]
    [InlineData("@find_block_leaders")]
    public void LegitParameter_IsNotBlocked(string parameterName)
    {
        Assert.DoesNotContain(WhoIsActiveService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────────────────────────────
    // Server resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task WhoIsActive_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteWhoIsActiveAsync("nonexistent", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("list_servers", ex.Message);
    }

    // ───────────────────────────────────────────────
    // Whitelist has expected count
    // ───────────────────────────────────────────────

    [Fact]
    public void AllowedProcedures_HasExactly1Entry()
    {
        Assert.Single(WhoIsActiveService.AllowedProcedures);
    }

    [Fact]
    public void BlockedParameters_HasExpectedCount()
    {
        Assert.Equal(4, WhoIsActiveService.BlockedParameters.Length);
    }
}
