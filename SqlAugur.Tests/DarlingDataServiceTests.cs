using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlAugur.Configuration;
using SqlAugur.Services;

namespace SqlAugur.Tests;

public class DarlingDataServiceTests
{
    private readonly DarlingDataService _service;

    public DarlingDataServiceTests()
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

        _service = new DarlingDataService(options, NullLogger<DarlingDataService>.Instance);
    }

    // ───────────────────────────────────────────────
    // Allowed procedure whitelist
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("sp_PressureDetector")]
    [InlineData("sp_QuickieStore")]
    [InlineData("sp_HealthParser")]
    [InlineData("sp_LogHunter")]
    [InlineData("sp_HumanEventsBlockViewer")]
    [InlineData("sp_IndexCleanup")]
    [InlineData("sp_QueryReproBuilder")]
    public void AllowedProcedure_IsInWhitelist(string procedureName)
    {
        Assert.Contains(procedureName, DarlingDataService.AllowedProcedures);
    }

    [Theory]
    [InlineData("SP_PRESSUREDETECTOR")]
    [InlineData("sp_pressuredetector")]
    [InlineData("Sp_QuickieStore")]
    [InlineData("SP_HEALTHPARSER")]
    public void AllowedProcedure_CaseInsensitive(string procedureName)
    {
        Assert.Contains(procedureName, DarlingDataService.AllowedProcedures);
    }

    [Theory]
    [InlineData("sp_who")]
    [InlineData("sp_help")]
    [InlineData("xp_cmdshell")]
    [InlineData("sp_MSforeachdb")]
    [InlineData("sp_executesql")]
    [InlineData("sp_Blitz")]
    [InlineData("sp_WhoIsActive")]
    public void DisallowedProcedure_IsNotInWhitelist(string procedureName)
    {
        Assert.DoesNotContain(procedureName, DarlingDataService.AllowedProcedures);
    }

    // ───────────────────────────────────────────────
    // Blocked parameters
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("@log_to_table")]
    [InlineData("@log_database_name")]
    [InlineData("@log_schema_name")]
    [InlineData("@log_table_name_prefix")]
    [InlineData("@log_retention_days")]
    [InlineData("@output_database_name")]
    [InlineData("@output_schema_name")]
    [InlineData("@delete_retention_days")]
    public void BlockedParameter_IsInBlockList(string parameterName)
    {
        Assert.Contains(DarlingDataService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("@sort_order")]
    [InlineData("@database_name")]
    [InlineData("@expert_mode")]
    [InlineData("@top")]
    [InlineData("@what_to_check")]
    [InlineData("@sample_seconds")]
    [InlineData("@days_back")]
    [InlineData("@custom_message")]
    [InlineData("@gimme_danger")]
    public void LegitParameter_IsNotBlocked(string parameterName)
    {
        Assert.DoesNotContain(DarlingDataService.BlockedParameters,
            blocked => parameterName.Equals(blocked, StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────────────────────────────
    // Server resolution
    // ───────────────────────────────────────────────

    [Fact]
    public async Task PressureDetector_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecutePressureDetectorAsync("nonexistent", null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("list_servers", ex.Message);
    }

    [Fact]
    public async Task QuickieStore_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteQuickieStoreAsync("bad", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task HealthParser_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteHealthParserAsync("bad", null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task LogHunter_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteLogHunterAsync("bad", null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task HumanEventsBlockViewer_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteHumanEventsBlockViewerAsync("bad", null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task IndexCleanup_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteIndexCleanupAsync("bad", null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public async Task QueryReproBuilder_UnknownServer_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteQueryReproBuilderAsync("bad", null, null, null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None));
        Assert.Contains("bad", ex.Message);
    }

    // ───────────────────────────────────────────────
    // Whitelist has expected count
    // ───────────────────────────────────────────────

    [Fact]
    public void AllowedProcedures_HasExactly7Entries()
    {
        Assert.Equal(7, DarlingDataService.AllowedProcedures.Count);
    }

    [Fact]
    public void BlockedParameters_HasExpectedCount()
    {
        Assert.Equal(8, DarlingDataService.BlockedParameters.Length);
    }
}
