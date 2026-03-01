using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;
using SqlAugur.Services;

namespace SqlAugur.Tests;

public class FirstResponderServiceTests
{
    private readonly FirstResponderService _service;

    public FirstResponderServiceTests()
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

        _service = new FirstResponderService(options, NullLogger<FirstResponderService>.Instance);
    }

    // ───────────────────────────────────────────────
    // Allowed procedure whitelist
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("sp_Blitz")]
    [InlineData("sp_BlitzFirst")]
    [InlineData("sp_BlitzCache")]
    [InlineData("sp_BlitzIndex")]
    [InlineData("sp_BlitzWho")]
    [InlineData("sp_BlitzLock")]
    public void AllowedProcedure_IsInWhitelist(string procedureName)
    {
        Assert.Contains(procedureName, FirstResponderService.AllowedProcedures);
    }

    [Theory]
    [InlineData("SP_BLITZ")]
    [InlineData("sp_blitz")]
    [InlineData("Sp_BlitzFirst")]
    public void AllowedProcedure_CaseInsensitive(string procedureName)
    {
        Assert.Contains(procedureName, FirstResponderService.AllowedProcedures);
    }

    [Theory]
    [InlineData("sp_who")]
    [InlineData("sp_help")]
    [InlineData("xp_cmdshell")]
    [InlineData("sp_MSforeachdb")]
    [InlineData("sp_executesql")]
    [InlineData("sp_BlitzBackups")]
    public void DisallowedProcedure_IsNotInWhitelist(string procedureName)
    {
        Assert.DoesNotContain(procedureName, FirstResponderService.AllowedProcedures);
    }

    // ───────────────────────────────────────────────
    // Blocked output parameters
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("@OutputDatabaseName")]
    [InlineData("@OutputSchemaName")]
    [InlineData("@OutputTableName")]
    [InlineData("@OutputServerName")]
    [InlineData("@OutputTableNameFileStats")]
    [InlineData("@OutputTableNamePerfmonStats")]
    [InlineData("@OutputTableNameWaitStats")]
    [InlineData("@OutputTableRetentionDays")]
    public void BlockedParameter_IsInBlockList(string parameterName)
    {
        Assert.Contains(FirstResponderService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("@SortOrder")]
    [InlineData("@DatabaseName")]
    [InlineData("@ExpertMode")]
    [InlineData("@Seconds")]
    [InlineData("@Top")]
    [InlineData("@CheckServerInfo")]
    public void LegitParameter_IsNotBlocked(string parameterName)
    {
        Assert.DoesNotContain(FirstResponderService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────────────────────────────
    // Server resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task Blitz_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzAsync("nonexistent", null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("list_servers", ex.Message);
    }

    [Fact]
    public async Task BlitzFirst_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzFirstAsync("bad", null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task BlitzCache_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzCacheAsync("bad", null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task BlitzIndex_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzIndexAsync("bad", null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task BlitzWho_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzWhoAsync("bad", null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task BlitzLock_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteBlitzLockAsync("bad", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    // ───────────────────────────────────────────────
    // Whitelist has expected count
    // ───────────────────────────────────────────────

    [Fact]
    public void AllowedProcedures_HasExactly6Entries()
    {
        Assert.Equal(6, FirstResponderService.AllowedProcedures.Count);
    }

    [Fact]
    public void BlockedParameters_HasExpectedCount()
    {
        Assert.Equal(8, FirstResponderService.BlockedParameters.Length);
    }
}
