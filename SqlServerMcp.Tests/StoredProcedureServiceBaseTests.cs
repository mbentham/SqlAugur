using SqlServerMcp.Services;

namespace SqlServerMcp.Tests;

public class StoredProcedureServiceBaseTests
{
    // ───────────────────────────────────────────────
    // FormatValue — DateTime
    // ───────────────────────────────────────────────

    [Fact]
    public void FormatValue_DateTime_ReturnsRoundTripFormat()
    {
        var dt = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var result = StoredProcedureServiceBase.FormatValue(dt);
        Assert.IsType<string>(result);
        Assert.Equal(dt.ToString("O"), (string)result);
    }

    [Fact]
    public void FormatValue_DateTime_Unspecified_ReturnsRoundTripFormat()
    {
        var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var result = StoredProcedureServiceBase.FormatValue(dt);
        Assert.Equal(dt.ToString("O"), (string)result);
    }

    // ───────────────────────────────────────────────
    // FormatValue — DateTimeOffset
    // ───────────────────────────────────────────────

    [Fact]
    public void FormatValue_DateTimeOffset_ReturnsRoundTripFormat()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.FromHours(-5));
        var result = StoredProcedureServiceBase.FormatValue(dto);
        Assert.IsType<string>(result);
        Assert.Equal(dto.ToString("O"), (string)result);
    }

    [Fact]
    public void FormatValue_DateTimeOffset_Utc_ReturnsRoundTripFormat()
    {
        var dto = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var result = StoredProcedureServiceBase.FormatValue(dto);
        Assert.Equal(dto.ToString("O"), (string)result);
    }

    // ───────────────────────────────────────────────
    // FormatValue — byte[]
    // ───────────────────────────────────────────────

    [Fact]
    public void FormatValue_ByteArray_ReturnsBase64String()
    {
        var bytes = new byte[] { 0x00, 0xFF, 0x42, 0xAB };
        var result = StoredProcedureServiceBase.FormatValue(bytes);
        Assert.IsType<string>(result);
        Assert.Equal(Convert.ToBase64String(bytes), (string)result);
    }

    [Fact]
    public void FormatValue_EmptyByteArray_ReturnsEmptyBase64()
    {
        var bytes = Array.Empty<byte>();
        var result = StoredProcedureServiceBase.FormatValue(bytes);
        Assert.Equal(string.Empty, (string)result);
    }

    // ───────────────────────────────────────────────
    // FormatValue — pass-through (default branch)
    // ───────────────────────────────────────────────

    [Fact]
    public void FormatValue_Int_ReturnsUnchanged()
    {
        var result = StoredProcedureServiceBase.FormatValue(42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void FormatValue_String_ReturnsUnchanged()
    {
        var result = StoredProcedureServiceBase.FormatValue("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void FormatValue_Decimal_ReturnsUnchanged()
    {
        var result = StoredProcedureServiceBase.FormatValue(3.14m);
        Assert.Equal(3.14m, result);
    }

    [Fact]
    public void FormatValue_Bool_ReturnsUnchanged()
    {
        var result = StoredProcedureServiceBase.FormatValue(true);
        Assert.Equal(true, result);
    }

    [Fact]
    public void FormatValue_Guid_ReturnsUnchanged()
    {
        var guid = Guid.NewGuid();
        var result = StoredProcedureServiceBase.FormatValue(guid);
        Assert.Equal(guid, result);
    }

    [Fact]
    public void FormatValue_Long_ReturnsUnchanged()
    {
        var result = StoredProcedureServiceBase.FormatValue(9999999999L);
        Assert.Equal(9999999999L, result);
    }
}
